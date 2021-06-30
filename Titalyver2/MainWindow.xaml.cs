﻿using System;
using System.Windows;
using System.Windows.Input;

namespace Titalyver2
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {


        private readonly Receiver Receiver;

        private readonly LyricsSearcher LyricsSearcher;


        public MainWindow()
        {
            InitializeComponent();

            KaraokeDisplay.SetLyrics("");
            LyricsSearcher = new LyricsSearcher();

            Receiver = new Receiver(PlaybackEvent);
            Message.Data data = Receiver.GetData();
            if (data.PlaybackEvent == Message.EnumPlaybackEvent.NULL)
                return;
            string lyrics =  GetLyrics(data);
            KaraokeDisplay.SetLyrics(lyrics);
            if (lyrics != "")
            {
                double delay = Message.GetTimeOfDay() - data.TimeOfDay / 1000.0;
                KaraokeDisplay.ForceMove(delay + data.SeekTime, 0.5);
                KaraokeDisplay.Time = delay + data.SeekTime;
                if ((data.PlaybackEvent & Message.EnumPlaybackEvent.PlayNew) == Message.EnumPlaybackEvent.PlayNew )
                    KaraokeDisplay.Start();
            }
        }
        private string GetLyrics(Receiver.Data data)
        {
            Uri uri = new(data.FilePath);
            string lp = uri.LocalPath;

            string text = LyricsSearcher.Search(lp, data.MetaData);
            if (text == "")
                return "";
            return text;

        }

        private void PlaybackEvent(Receiver.Data data)
        {

            double delay = (Receiver.GetTimeOfDay() - data.TimeOfDay) / 1000.0;

            switch (data.PlaybackEvent)
            {
                case Message.EnumPlaybackEvent.PlayNew:
                    {
                        Uri uri = new(data.FilePath);
                        string lp = uri.LocalPath;

                        string text = LyricsSearcher.Search(lp, data.MetaData);
                        if (text == "")
                            break;
                        _ = Dispatcher.InvokeAsync(() =>
                        {
                            KaraokeDisplay.SetLyrics(text);
                            KaraokeDisplay.Time = (Message.GetTimeOfDay() - data.TimeOfDay) / 1000.0;
                            KaraokeDisplay.Start();
                            KaraokeDisplay.ForceMove(delay, 0.5);
                        });
                        break;
                    }
                case Message.EnumPlaybackEvent.Stop:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        KaraokeDisplay.Stop();
                    });
                    break;
                case Message.EnumPlaybackEvent.PauseCancel:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        KaraokeDisplay.Time = data.SeekTime + delay;
                        KaraokeDisplay.Start();
                    });
                    break;
                case Message.EnumPlaybackEvent.Pause:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        KaraokeDisplay.Stop();
                        KaraokeDisplay.Time = data.SeekTime;
                    });
                    break;
                case Message.EnumPlaybackEvent.SeekPlaying:
                    _ = Dispatcher.InvokeAsync(() =>
                    {
                        KaraokeDisplay.Time = data.SeekTime + delay;
                        KaraokeDisplay.Start();
                        KaraokeDisplay.ForceMove(data.SeekTime + delay, 0.5);
                    });
                    break;
                case Message.EnumPlaybackEvent.SeekPause:
                    _ = Dispatcher.InvokeAsync(() =>

                    {
                        KaraokeDisplay.Stop();
                        KaraokeDisplay.Time = data.SeekTime;
                        KaraokeDisplay.ForceMove(data.SeekTime, 0.5);
                    });
                    break;
            }

        }


        private void CloseButton_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private void Window_MouseWheel(object sender, MouseWheelEventArgs e)
        {
            KaraokeDisplay.ManualScrollY += e.Delta;
        }

        private void Window_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            DragMove();
        }

        private void Window_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.MiddleButton == MouseButtonState.Pressed)
            {
                KaraokeDisplay.ManualScrollY = 0;
            }
        }

        private void SettingButton_Click(object sender, RoutedEventArgs e)
        {

            SettingsWindow SettingWindow = new SettingsWindow(this);
            SettingWindow.Show();
        }
    }
}
