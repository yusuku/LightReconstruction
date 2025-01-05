# LightReconstruction
## 概要
空中立体映像を、人などのオブジェクトとのインタラクション時によりリアルに見えるように、現実環境に応じた陰影を空中立体映像に付与する。
リアルタイムに陰影を付与し、映像と光源間に遮蔽がある場合の影も再現している。これらを考慮したシステムは調べたところまだない。

## モチベーション
仮想的なものと現実の境界をなくし、現実ではありえないような現象を提供したい。

## 方法
以下の２つに分けて実装に取り組んでいる。
- 3DEnvReconstruction: 360度カメラ画像から3Dシーン再構築
- LightEnvReconstruction：　360度カメラ画像から光源環境再構築
「現実の光源環境再構築」により現実の光源を取り出し、 「現実の3Dシーン再構築」により映像と光源間の遮蔽を再現し、映像に陰影を生成する。

空中立体映像に、現実環境の光源に対応する陰影を与えることはまだ研究されていない。　現実光源環境の再構築は、XRの先行研究があるが、カメラから光源が見えているという前提があり、カメラ画像に光源がない、つまりカメラと光源間に何か遮蔽がある場合に対応しきれていない。この解決策に360度カメラ画像から3Dシーンを再構築することを採用している。



## 3DEnvReconstruction
### 概要
360度画像から、各ピクセルの深度を推定し、周辺環境の３Dシーンを再構築する。


AIモデル ：　Midas https://github.com/isl-org/MiDaS

onnx ファイル：　https://huggingface.co/julienkay/sentis-MiDaS/tree/main/onnx
### 結果
|元画像|3Dシーン再構築|
|---|---|
|![スクリーンショット 2025-01-05 123058](https://github.com/user-attachments/assets/93f8df68-7123-4cc0-868a-4c460447c7f7)|![スクリーンショット 2025-01-05 122644](https://github.com/user-attachments/assets/28eaf3aa-78ef-411a-b27f-b45486394122)|

|深度画像（遠：黒、近：赤）|3Dシーン再構築（別視点）|
|---|---|
|![スクリーンショット 2025-01-05 122920](https://github.com/user-attachments/assets/a7493828-5a37-4f77-9b74-8f4ee3c68a9b)|![スクリーンショット 2025-01-05 122607](https://github.com/user-attachments/assets/bc113c5d-dda3-4c10-8dbb-93a3316b40f4)|

### 方法
#### 深度推定 RunAIModel/MidasModel.cs
- Unity.sentisを使用。
- 深度推定モデルMidasを使用し、入力画像から深度画像を推定、画像ピクセルの最大最小値を使用し正規化し出力。
- モデルonnx : https://huggingface.co/julienkay/sentis-MiDaS。最高精度のモデルより少し精度は劣るが、推論の速さや使用メモリが少ない。　最高はメモリが多くUnityに入れにくい。低いものは、精度が落ち、3Dシーン再構築に向かない。

#### コード解説　MidasModel.cs
1. AIモデルを読み込む
2. 入力画像をAIモデルの入力形状のテンソルに直す。
3. 推論
4. 出力深度画像をピクセル最大最小値を使用し正規化。
5. 次に、入力画像サイズに直す

### 苦労した点、工夫した点：
- ＡＩモデルをUnityで扱う方法から調べ始めた点。
- pytorchのＡＩモデルを自分の目的に合わせて探していった点。
- pytorchモデルをどのようにしてOnnxファイルに変換するかを考えた点。
- pytorchモデルの入出力サイズと値幅が、自分の望むものではないので、pytorch側でモデルにレイヤーを足すか、Unity.sentis側で処理するかを試した点。
- デバックのためにテクスチャを用意した点。



3Dシーン再構築

## LightEnvReconstruction
### 概要

360度画像から光源の方向、強度、色を推定し、推定された各光源情報をUnityのDirectional Lightに割り当て、画像の光源環境を再構築する。

### 結果

背景とパネルに乗せた３６０度画像から光源環境を再構築した。
|元画像と結果| 元画像と結果(別画像）|
|---|---|
|![image](https://github.com/user-attachments/assets/cfe2c8a8-03bc-4e9b-9236-2f2e160fdfc1)|![image](https://github.com/user-attachments/assets/b90762e7-e305-46cd-a198-0c0e0346bc4f)|


### 方法

[1]のIBS(Image Based Shading)をUnityに転用し,LightEnvReconstruction/CPURunEstimation.csにおいてLightEstimationというクラスを実装した。

#### LightEstimationクラス:
- LightEstimation.LightEstimation：コンストラクタ　　入力画像テクスチャ（LDRtex)を設定する。
- LightEstimation.estimation(): 光源情報を推定する。
- LightEstimation.ReconstructLights(): 推定された光源情報をもとにライトを作成する。


#### 光源推定[1]LightEstimation.estimation()の手順：
1. 入力画像を以下の式でHDR画像に変換する。　LightEstimation.InverseToneMapping()
2. HDR画像の輝度を計算。計算した輝度の、平均＋標準偏差＊２、を光源判断に使用する閾値に設定。LightEstimation.SetThresholdingLuminances()
3. 幅優先探索で２.で計算した閾値以上のピクセルの連結部分を各光源として、画像ピクセル上にラベルを付ける。LightEstimation.BreathfirstSearch()
4. 各ピクセルの立体角、HDRピクセル値、輝度を用いて各ピクセルの放射照度（そのピクセルの立体角からどのくらいのエネルギーが目に入るか示す）を求める ：Irradiance。また3.で分けた各光源の放射照度を合計したものを配列Elsに格納する。：LightEstimation.IrradianceSetting()
5. 3.で分けた各光源に対して、ピクセルの放射照度を重みとして、ピクセル位置の極座標の重みづけ平均を取り、その極座標を、各光源の方向とする。：LightEstimation.LightPosition()
6. 最後に、各光源の放射照度のRGB値を光源色に、放射照度を放射輝度に変換したものを光源強度にあてる。最終的なLightEstimation.estimation()の出力は、光源極座標、色、強度　の３つになる


#### 光源環境の再構築LightEstimation.ReconstructLights()の手順:
1. LightEstimation.estimation()で計算した光源強度が0.1以上の光源情報を、DirectionalLightのcolor , intensity, position に充てる。positionについて、LightEstimation.estimation()の出力では光源の極座標しか出ないので、３D座標系に変換したものをpositionに充てる。


### 苦労した点、工夫した点：
- [1]の論文を見つけてくるまでに、１０本ほど論文をよんで、自分の研究に何が必要かを考え、この論文に至った点。
- [1]の論文で、研究に必要な部分を抽出し、コード化した点。
- コード化では、できるだけ論文の記述順序に沿って、シンプルに関数を並べられるようにした点。
- 最終的な出力結果のリアルタイム性を考慮し、計算負荷を小さくするために以下の点を工夫した点
  - LightEstimation.InverseToneMapping()内でCompute Shaderを使用した。
  - LightEstimation.ReconstructLights()内では、ライトを生成する回数を最小限にするために、LightEstimation.estimation()で計算された、光源の個数が増えた時にだけライトを生成し、そのほかの場合はただライトの情報を書き換えるようにしている。






### 参照
[1]Taehyun Rhee, Member, IEEE, Lohit Petikam, Benjamin Allen, and Andrew Chalmers,MR360: Mixed Reality Rendering for 360° Panoramic Videos,IEEE TRANSACTIONS ON VISUALIZATION AND COMPUTER GRAPHICS, VOL. 23, NO. 4, APRIL 2017


