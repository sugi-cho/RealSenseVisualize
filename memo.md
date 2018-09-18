# memo

## voxel particle

| name | type | disp |
| --- | --- | --- |
| vert  | float3 | 深度データから取得したtriangleのcenter |
| pos   | float3 | パーティクルの位置 |
| vel   | float3 | パーティクルの速度(使用しない時もある) |
| dir   | float3 | パーティクルの方向 |
| prop  | float4 | パーティクルの状態、動作に影響する |
| t     | float  | 時間t(-x~1.0) |
| size  | float  | パーティクルの大きさ(0~x) |

## voxelParticle.prop

- x
  - 対象のvertが大きく動いたかどうか
- y
  - floating mode　通常は、上方向へ浮かんでいく、pos = pos+vel
- z
  - 光るパーティクルモード : ふわふわ光りながら消える
- w
  - 崩れるパーティクルモード : 重力で、落ちていく

## kernels

### init

パーティクルの初期化。表示がおかしくなった時も呼ぶといい

### build

深度データからパーティクルを作る。プロパティをもとに位置を更新する

## Control

- voxelをばらばらにする
- 動きを一時停止
- 全体を回転させる
- 位置のリセット
- ライトを動かす
- ライトのOn-Off
- カメラを動かす
- カメラの焦点距離の変更

### Motion Controller

- grab
- trigger
- stick
- menu
- pad.xy/pad.in

#### Left

- ライトの移動 grab
- ライトの色を変更 pad.xy
- ライトを上空に設置し、コントローラの位置を照らす pad.in
- 全体の回転（回転の中心はカメラの中心、焦点位置）stick.x
- 範囲内のvoxelをバラバラにする trigger
- エフェクトを加える menu

#### Right

- カメラの移動 grab
- カメラの焦点距離を変更 pad.y
- カメラの焦点位置を設定し、そこから光パーティクルが出る pad.in
- 全体の移動 stick.xy
- 動きをPause trigger
- リセット menu

## scene

- ワイヤーフレームだけ
- バックライト
- ライティングON
- 動きでエフェクト出るようになる