﻿using System;
using System.Collections.Generic;
using System.Text;

using System.IO;
using System.IO.MemoryMappedFiles;
using System.Threading;
using System.Diagnostics;

using System.Text.Json;


namespace Titalyver2
{
    public class MMFMessage
    {
        protected const UInt32 MMF_MaxSize = 1024 * 1024 * 64;

        protected const string MMF_Name = "Titalyver Message Data MMF";
        protected const string WriteEvent_Name = "Titalyver Message Write Event";
        protected const string Mutex_Name = "Titalyver Message Mutex";

        protected Mutex Mutex;
        protected EventWaitHandle EventWaitHandle;


        public bool IsValid() { return Mutex != null; }

        public static int GetTimeOfDay()
        {
            DateTime now = DateTime.Now;
            return ((now.Hour * 60 + now.Minute) * 60 + now.Second) * 1000 + now.Millisecond;
        }


        protected bool Initialize()
        {
            Terminalize();
            try
            {
                Mutex = new Mutex(false, Mutex_Name);
                EventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, WriteEvent_Name);
            }
            catch (Exception e)
            {
                Terminalize();
                Debug.WriteLine(e.Message);
                return false;
            }
            return true;
        }

        protected void Terminalize()
        {
            EventWaitHandle?.Dispose();
            EventWaitHandle = null;

            Mutex?.Dispose();
            Mutex = null;
        }


        public MMFMessage() {}

        ~MMFMessage() { Terminalize(); }

        protected class MutexLock : IDisposable
        {
            private Mutex Mutex;
            public bool Result { get; private set; }
            public MutexLock(Mutex mutex, int timeout_millisec)
            {
                Mutex = mutex;
                Result = mutex.WaitOne(timeout_millisec);
            }
            public void Unlock()
            {
                Mutex?.ReleaseMutex();
                Mutex = null;
            }

            protected virtual void Dispose(bool disposing)
            {
                if (disposing)
                {
                    // TODO: マネージド状態を破棄します (マネージド オブジェクト)
                }
                if (Result)
                {
                    Mutex.ReleaseMutex();
                    Result = false;
                }
            }

            // // TODO: 'Dispose(bool disposing)' にアンマネージド リソースを解放するコードが含まれる場合にのみ、ファイナライザーをオーバーライドします
            ~MutexLock()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: false);
            }

            public void Dispose()
            {
                // このコードを変更しないでください。クリーンアップ コードを 'Dispose(bool disposing)' メソッドに記述します
                Dispose(disposing: true);
                GC.SuppressFinalize(this);
            }
        }
    };

    public class MMFMessenger : MMFMessage
    {
	    private MemoryMappedFile MemoryMappedFile;
        public new bool Initialize()
        {
            if (base.Initialize())
            {
                using (MutexLock ml = new MutexLock(Mutex, 100))
                {
                    if (!ml.Result)
                    {
                        ml.Unlock();
                        Terminalize();
                        return false;
                    }

                    try
                    {
                        try
                        {
                            using (MemoryMappedFile test = MemoryMappedFile.OpenExisting(MMF_Name))
                            {
                                ml.Unlock();
                                Terminalize();
                                return false;
                            }
                        }
                        catch (FileNotFoundException) { }
                        MemoryMappedFile = MemoryMappedFile.CreateOrOpen(MMF_Name, MMF_MaxSize, MemoryMappedFileAccess.ReadWrite);
                    }
                    catch (Exception e)
                    {
                        ml.Unlock();
                        Terminalize();
                        Debug.WriteLine(e.Message);
                        return false;
                    }
                    return true;
                }
            }
            return false;

        }
        public new void Terminalize()
        {
            MemoryMappedFile?.Dispose();
            MemoryMappedFile = null;
            base.Terminalize();
        }

        public bool Update(ITitalyverReceiver.EnumPlaybackEvent pbevent, double seektime, byte[] json)
        {
            int size = 4 + 8 + 4 + 4 + 4 + json.Length;

            using (MutexLock ml = new MutexLock(Mutex, 100))
            {
                if (!ml.Result)
                    return false;
                using (MemoryMappedViewAccessor mmva = MemoryMappedFile.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite))
                {
                    Int32 timeofday = GetTimeOfDay();
                    Int64 offset = 0;
                    mmva.Write(offset, (Int32)pbevent); offset += 4;
                    mmva.Write(offset, seektime); offset += 8;
                    mmva.Write(offset, timeofday); offset += 4;
                    mmva.Write(offset, timeofday); offset += 4;
                    mmva.Write(offset, json.Length); offset += 4;
                    mmva.WriteArray(offset, json, 0, json.Length);
                }

                _ = EventWaitHandle.Set();
            }
            return true;
        }
	    public bool Update(ITitalyverReceiver.EnumPlaybackEvent pbevent, double seektime)
        {
            int size = 4 + 8 + 4;

            using (MutexLock ml = new MutexLock(Mutex, 100))
            {
                if (!ml.Result)
                    return false;
                using (MemoryMappedViewAccessor mmva = MemoryMappedFile.CreateViewAccessor(0, size, MemoryMappedFileAccess.ReadWrite))
                {
                    Int64 offset = 0;
                    mmva.Write(offset, (Int32)pbevent); offset += 4;
                    mmva.Write(offset, seektime); offset += 8;
                    mmva.Write(offset, GetTimeOfDay());
                }
                _ = EventWaitHandle.Set();
            }
            return true;
        }



        public MMFMessenger() { }
        ~MMFMessenger() { Terminalize(); }

    }


    public class MMFReceiver : MMFMessage,ITitalyverReceiver
    {
        public event ITitalyverReceiver.PlaybackEventHandler OnPlaybackEventChanged;

        public bool Updated { get { return LastUpdateTimeOfDay != data.UpdateTimeOfDay; } }

        private RegisteredWaitHandle RegisteredWaitHandle;

        private Int32 LastUpdateTimeOfDay = -1;

        public ITitalyverReceiver.Data GetData() { return data; }

        private ITitalyverReceiver.Data data;
        public bool ReadData()
        {
            byte[] buffer;
            try
            {
                using MutexLock ml = new(Mutex, 100);
                if (!ml.Result)
                    return false;
                using MemoryMappedFile mmf = MemoryMappedFile.OpenExisting(MMF_Name, MemoryMappedFileRights.Read);
                using MemoryMappedViewStream stream = mmf.CreateViewStream(0, 0, MemoryMappedFileAccess.Read);
                byte[] bytes = new byte[24];
                int read = stream.Read(bytes, 0, 24);
                data.PlaybackEvent = (ITitalyverReceiver.EnumPlaybackEvent)BitConverter.ToUInt32(bytes, 0);
                data.SeekTime = BitConverter.ToDouble(bytes, 4);
                data.TimeOfDay = BitConverter.ToInt32(bytes, 12);
                data.UpdateTimeOfDay = BitConverter.ToInt32(bytes, 16);
                Int32 json_size = BitConverter.ToInt32(bytes, 20);

                if (LastUpdateTimeOfDay == data.UpdateTimeOfDay)
                {
                    data.MetaDataUpdated = false;
                    return true;
                }
                buffer = new byte[json_size];
                _ = stream.Read(buffer, 0, json_size);
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                return false;
            }

            if (data.MetaData == null)
                data.MetaData = new Dictionary<string, string[]>();
            else
                data.MetaData.Clear();
            data.FilePath = "";
            try
            {
                using JsonDocument document = JsonDocument.Parse(buffer);
                if (document.RootElement.ValueKind != JsonValueKind.Object)
                {
                    data.MetaData = null;
                    return false;
                }
                data.FilePath = document.RootElement.GetProperty("path").GetString();
                JsonElement meta = document.RootElement.GetProperty("meta");
                foreach (JsonProperty e in meta.EnumerateObject())
                {
                    switch (e.Value.ValueKind)
                    {
                        case JsonValueKind.String:
                            data.MetaData.Add(e.Name.ToLower(null), new string[] { e.Value.GetString() });
                            break;
                        case JsonValueKind.Array:
                            string[] array = new string[e.Value.GetArrayLength()];
                            int i = 0;
                            foreach (JsonElement a in e.Value.EnumerateArray())
                            {
                                array[i++] = a.ValueKind == JsonValueKind.String ? a.GetString() : a.GetRawText();
                            }
                            data.MetaData.Add(e.Name.ToLower(null), array);
                            break;
                        default:
                            data.MetaData.Add(e.Name.ToLower(null), new string[] { e.Value.GetRawText() });
                            break;
                    }
                }
            }
            catch (Exception e)
            {
                Debug.WriteLine(e.Message);
                data.MetaData = null;
                return false;
            }
            LastUpdateTimeOfDay = data.UpdateTimeOfDay;
            data.MetaDataUpdated = true;
            return true;
        }

        public MMFReceiver(ITitalyverReceiver.PlaybackEventHandler playbackEventHandler)
        {
            if (!base.Initialize())
                return;
            OnPlaybackEventChanged += playbackEventHandler;
            EventWaitHandle = new EventWaitHandle(false, EventResetMode.AutoReset, WriteEvent_Name);
            RegisteredWaitHandle = ThreadPool.RegisterWaitForSingleObject(EventWaitHandle, WaitOrTimerCallback, null, -1, false);
        }
        ~MMFReceiver()
        {
            Terminalize();
        }
        private void WaitOrTimerCallback(object state, bool timedOut)
        {
            if (!timedOut)
            {
                if (ReadData())
                    OnPlaybackEventChanged?.Invoke(data);
            }
        }

        void ITitalyverReceiver.Terminalize()
        {
            _ = RegisteredWaitHandle?.Unregister(EventWaitHandle);
            RegisteredWaitHandle = null;
            base.Terminalize();
        }
    }
}
