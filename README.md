
# LightEnvReconstruction
概要

360度画像から光源の方向、強度、色を推定し、推定された各光源情報をUnityのDirectional Lightに割り当て、画像の光源環境を再構築する。

結果

背景とパネルに乗せた３６０度画像から光源環境を再構築した。
![image](https://github.com/user-attachments/assets/cfe2c8a8-03bc-4e9b-9236-2f2e160fdfc1)
![image](https://github.com/user-attachments/assets/b90762e7-e305-46cd-a198-0c0e0346bc4f)


方法

[1]のIBS(Image Based Shading)をUnityに転用し,LightEnvReconstruction/CPURunEstimation.csにおいてLightEstimationというクラスを実装した。

LightEstimationクラス:
- LightEstimation.LightEstimation：コンストラクタ　　入力画像テクスチャ（LDRtex)を設定する。
- LightEstimation.estimation(): 光源情報を推定する。
- LightEstimation.ReconstructLights(): 推定された光源情報をもとにライトを作成する。


光源推定[1]LightEstimation.estimation()の手順：
1. 入力画像を以下の式でHDR画像に変換する。　LightEstimation.InverseToneMapping()
2. HDR画像の輝度を計算。計算した輝度の、平均＋標準偏差＊２、を光源判断に使用する閾値に設定。LightEstimation.SetThresholdingLuminances()
3. 幅優先探索で２.で計算した閾値以上のピクセルの連結部分を各光源として、画像ピクセル上にラベルを付ける。LightEstimation.BreathfirstSearch()
4. 各ピクセルの立体角、HDRピクセル値、輝度を用いて各ピクセルの放射照度（そのピクセルの立体角からどのくらいのエネルギーが目に入るか示す）を求める ：Irradiance。また3.で分けた各光源の放射照度を合計したものを配列Elsに格納する。：LightEstimation.IrradianceSetting()
5. 3.で分けた各光源に対して、ピクセルの放射照度を重みとして、ピクセル位置の極座標の重みづけ平均を取り、その極座標を、各光源の方向とする。：LightEstimation.LightPosition()
[1]Taehyun Rhee, Member, IEEE, Lohit Petikam, Benjamin Allen, and Andrew Chalmers,MR360: Mixed Reality Rendering for 360° Panoramic Videos,IEEE TRANSACTIONS ON VISUALIZATION AND COMPUTER GRAPHICS, VOL. 23, NO. 4, APRIL 2017


