
# Controls
- 電源(ON or OFF)
- 設定気温 16 ~ 31
- 運転モード切替
	- 冷房
	- 暖房
	- 除湿
		- 除湿調節(標準, 強, 弱)
	- 送風
- 風ロング(bool)
- 風速(自動, 1 ~ 3)
- 風上下(自動, 1 ~ 5, 首振り)
- 風左右 (1 ~ 5, 首振り)
- 風エリア(全体, 左, 右, オフ)
- タイマー(On, Off)(Max12, Min0.5, 0.5刻み)
- 内部クリーン(bool)

Format : AEHA(家製協フォーマット)

Off
23/cb/26/01/00/00/58/0a/26/40/00/00/00/00/10/00/00/ed
On(冷房) 26
23/cb/26/01/00/20/58/0a/26/40/00/00/00/00/10/00/00/0d
On(冷房) 26 ピピッ
23/cb/26/01/00/20/58/0a/26/80/00/00/00/01/10/00/00/4e
On(冷房) 27
23/cb/26/01/00/20/58/0b/26/40/00/00/00/00/10/00/00/0e
On(冷房) 27 切タイマ5h
23/cb/26/01/00/20/58/0a/26/40/00/1e/00/03/10/00/00/2e
On(冷房) 27 切タイマ4.5h
23/cb/26/01/00/20/58/0a/26/40/00/1b/00/03/10/00/00/2b
On(冷房) 27 切タイマ3.5h
23/cb/26/01/00/20/58/0a/26/40/00/15/00/03/10/00/00/25
On(冷房) 27 切タイマ1h
23/cb/26/01/00/20/58/0a/26/40/00/06/00/03/10/00/00/16
On(冷房) 27 切タイマ0.5h
23/cb/26/01/00/20/58/0a/26/80/00/03/00/03/10/00/00/53
On(冷房) 27 入タイマ6h
23/cb/26/01/00/20/58/0a/26/40/00/00/24/05/10/00/00/36
On(冷房) 25
23/cb/26/01/00/20/58/09/26/40/00/00/00/00/10/00/00/0c
除湿(On)
23/cb/26/01/00/20/50/08/22/40/00/00/00/00/10/00/00/ff
除湿(Off)
23/cb/26/01/00/00/50/08/22/40/00/00/00/00/10/00/00/df
暖房
23/cb/26/01/00/20/48/08/20/40/00/00/00/00/10/00/00/f5
送風
23/cb/26/01/00/20/38/08/20/40/00/00/00/00/10/00/00/e5
設定室温とタイマ設定時間は左が下位桁になる(修正済み)
# エアコンの制御コード

| byte | データ内容                                  | 例   |     |
| ---- | -------------------------------------- | --- | --- |
| 0    | Customer Code(固定)                      | 23  |     |
| 1    | Customer Code(固定)                      | cb  |     |
| 2    | Parity(固定) & Data0(多分固定)               | 26  |     |
| 3    |                                        | 01  |     |
| 4    |                                        | 00  |     |
| 5    | 00:Off 20:On                           | 20  |     |
| 6    | 58:冷房 50:除湿 48:暖房 送風:38                | 58  |     |
| 7    | 設定室温 データ = 設定室温 - 16                   | 0a  |     |
| 8    |                                        | 26  |     |
| 9    | 受信時の音 40: ピッ 80 : ピピッ                  | 40  |     |
| 10   |                                        | 00  |     |
| 11   | OFFタイマ時間0.5=3, タイマ時間 = 6 * 生の値 意★味★不★明 | 00  |     |
| 12   | ONタイマ時間0.5=3, タイマ時間 = 6 * 生の値          | 00  |     |
| 13   | 03 : OFFタイマ 05 : ONタイマ 00:タイマ指定なし      | 00  |     |
| 14   |                                        | 10  |     |
| 15   |                                        | 00  |     |
| 16   |                                        | 00  |     |
| 17   | エラー検出コード                               | 0d  |     |
|      |                                        |     |     |


6
0010 0100
5
0111 1000
4.5
1101 1000
4
0001 1000
3.5
1010 1000
1
0110 0000
0.5
1100 0000