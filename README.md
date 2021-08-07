# Titalyver2

TimeTagedLyricsViewer version 2


- lrcファイル、いわゆるタイムタグ付き歌詞ファイルを音楽に合わせて表示します。
- カラオケタグ(.kra)も扱えます。
- 独自仕様のルビ（振り仮名）表示に対応しています。
- 音楽再生機能は無いのでプラグイン機能のあるプレイヤーに専用プラグインを設置することでのみ機能します。
 （現在foobar2000(foo_titalyver_messenger)とMusicBee(mb_TitalyverMessenger)のみ）
- 今更iTunesにもとりあえず対応しました。 
- .NET 5.0で作ってます
- 操作方法等はwikiの方に https://github.com/Juna-Idler/Titalyver2/wiki

## デモ動画
- https://youtu.be/vsW0YLNFHT4 iTunes（21/7/20）
- https://youtu.be/q31rUxmlPFU MusicBee（21/7/6）


2ってあんまりつけたくなかったんだけど、ほかに変なものを付けるよりはまだましということで2

### wpfで文字をGeometry描画でやったらどうなるのか？
> 全画面更新はさすがにクソ重だったが、WPFの保持モードを活用して更新をアクティブな行に絞れば余裕で動く。
> ただ、やってることのわりに結構重い気もする。ちょっとCPU負荷がかかってる状態で動かすとフレーム落ちする。
> 文字数等にもよるけど開発環境で平均CPU30％ぐらい
> 描画を行単位から文字単位に分割すればもうちょっとはマシになるかもしれない。枠の重なりに弱くなるけど

とりあえずそこそこ動く状態まできたので、ちょっとだけREADMEを書き込む。
