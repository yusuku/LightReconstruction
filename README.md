# 3DEnvReconstruction
## 概要
360度画像から、各ピクセルの深度を推定し、周辺環境の３Dシーンを再構築する。

## 結果


# LightEnvReconstruction
## 概要

360度画像から光源の方向、強度、色を推定し、推定された各光源情報をUnityのDirectional Lightに割り当て、画像の光源環境を再構築する。

## 結果

背景とパネルに乗せた３６０度画像から光源環境を再構築した。
![image](https://github.com/user-attachments/assets/cfe2c8a8-03bc-4e9b-9236-2f2e160fdfc1)
![image](https://github.com/user-attachments/assets/b90762e7-e305-46cd-a198-0c0e0346bc4f)


## 方法

[1]のIBS(Image Based Shading)をUnityに転用し,LightEnvReconstruction/CPURunEstimation.csにおいてLightEstimationというクラスを実装した。

### LightEstimationクラス:
- LightEstimation.LightEstimation：コンストラクタ　　入力画像テクスチャ（LDRtex)を設定する。
- LightEstimation.estimation(): 光源情報を推定する。
- LightEstimation.ReconstructLights(): 推定された光源情報をもとにライトを作成する。


### 光源推定[1]LightEstimation.estimation()の手順：
1. 入力画像を以下の式でHDR画像に変換する。　LightEstimation.InverseToneMapping()
2. HDR画像の輝度を計算。計算した輝度の、平均＋標準偏差＊２、を光源判断に使用する閾値に設定。LightEstimation.SetThresholdingLuminances()
3. 幅優先探索で２.で計算した閾値以上のピクセルの連結部分を各光源として、画像ピクセル上にラベルを付ける。LightEstimation.BreathfirstSearch()
4. 各ピクセルの立体角、HDRピクセル値、輝度を用いて各ピクセルの放射照度（そのピクセルの立体角からどのくらいのエネルギーが目に入るか示す）を求める ：Irradiance。また3.で分けた各光源の放射照度を合計したものを配列Elsに格納する。：LightEstimation.IrradianceSetting()
5. 3.で分けた各光源に対して、ピクセルの放射照度を重みとして、ピクセル位置の極座標の重みづけ平均を取り、その極座標を、各光源の方向とする。：LightEstimation.LightPosition()
6. 最後に、各光源の放射照度のRGB値を光源色に、放射照度を放射輝度に変換したものを光源強度にあてる。最終的なLightEstimation.estimation()の出力は、光源極座標、色、強度　の３つになる


### 光源環境の再構築LightEstimation.ReconstructLights()の手順:
1. LightEstimation.estimation()で計算した光源強度が0.1以上の光源情報を、DirectionalLightのcolor , intensity, position に充てる。positionについて、LightEstimation.estimation()の出力では光源の極座標しか出ないので、３D座標系に変換したものをpositionに充てる。


## 苦労した点、工夫した点：
- [1]の論文を見つけてくるまでに、１０本ほど論文をよんで、自分の研究に何が必要かを考え、この論文に至った点。
- [1]の論文で、研究に必要な部分を抽出し、コード化した点。
- コード化では、できるだけ論文の記述順序に沿って、シンプルに関数を並べられるようにした点。
- 最終的な出力結果のリアルタイム性を考慮し、計算負荷を小さくするために以下の点を工夫した点
  - LightEstimation.InverseToneMapping()内でCompute Shaderを使用した。
  - LightEstimation.ReconstructLights()内では、ライトを生成する回数を最小限にするために、LightEstimation.estimation()で計算された、光源の個数が増えた時にだけライトを生成し、そのほかの場合はただライトの情報を書き換えるようにしている。






## 参照
[1]Taehyun Rhee, Member, IEEE, Lohit Petikam, Benjamin Allen, and Andrew Chalmers,MR360: Mixed Reality Rendering for 360° Panoramic Videos,IEEE TRANSACTIONS ON VISUALIZATION AND COMPUTER GRAPHICS, VOL. 23, NO. 4, APRIL 2017


