using System;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.Profiling;

public class MidasModel : MonoBehaviour
{
    // モデルとテクスチャの関連リソース
    public ModelAsset modelAsset;
    public Texture inputtex;

    public Material mat;


    MidasEstimation midas;

    void Start()
    {
        midas=new MidasEstimation(modelAsset, mat);
    }

    void Update()
    {
        midas.inference(inputtex);
        
    }

    void OnDisable()
    {
        midas.Release();
    }


}
public class MidasEstimation
{
    ModelAsset modelAsset;
    Worker m_Worker;
    Model model;
    Material mat;

    int width, height;

    public MidasEstimation(ModelAsset modelAsset,Material Debugmat)
    {
        this.modelAsset = modelAsset;
        this.model = ModelLoader.Load(modelAsset);
        this.m_Worker = new Worker(this.model, BackendType.GPUCompute);
        this.width = model.inputs[0].shape.Get(3);
        this.height = model.inputs[0].shape.Get(2);
        Debug.Log("input width:  "+this.width+" input_height: "+ this.height);
        Debug.Log(model.inputs[0].shape);
        this.mat=Debugmat;
        
    }

    RenderTexture ResizeRenderTexture(RenderTexture source, int newWidth, int newHeight)
    {
        // 新しい RenderTexture を作成
        RenderTexture newRenderTexture = new RenderTexture(newWidth, newHeight, source.depth, source.format);
        newRenderTexture.enableRandomWrite = source.enableRandomWrite; // 必要なら設定
        newRenderTexture.Create();

        // 古い RenderTexture の内容を新しい RenderTexture にコピー
        Graphics.Blit(source, newRenderTexture);

        return newRenderTexture;
    }

    private Tensor<float> NormalizeTensor(Tensor<float> input)
    {
        var cpuTensor = input.ReadbackAndClone();
        float max = float.MinValue;
        float min = float.MaxValue;

        for (int i = 0; i < cpuTensor.count; i++)
        {
            float value = cpuTensor[i];
            if (value > max) max = value;
            if (value < min) min = value;
        }
        for (int i = 0;i < cpuTensor.count; i++)
        {
            cpuTensor[i] = (cpuTensor[i] -min)/(max-min) ;
        }
        return cpuTensor;
    }

    public RenderTexture inference(Texture inputTexture)
    {
        RenderTexture outputRendertexture = null; // 初期化
        try
        {
            Profiler.BeginSample("This is Midas Process");
            
          

            // 入力テクスチャを Tensor に変換
            using (Tensor<float> inputTensor = TextureConverter.ToTensor(inputTexture,height: this.height, width: this.width, channels: 3))
            {
                // モデル推論のスケジューリング
                m_Worker.Schedule(inputTensor);

                // モデル出力を取得
                using (Tensor<float> outputTensor = m_Worker.PeekOutput() as Tensor<float>)
                {
                    if (outputTensor != null)
                    {
                        // 必要に応じて Tensor の形状を変更
                        //outputTensor.Reshape(new TensorShape(1, outputTensor.shape[0], outputTensor.shape[1], outputTensor.shape[2]));
                        Debug.Log($"Output Tensor Shape: {outputTensor.shape}");
                        if (outputTensor.shape.rank <4)
                        {
                            int size = outputTensor.shape.rank;
                            int outwidth= outputTensor.shape[size-1];
                            int outheight= outputTensor.shape[size-2];
                            outputTensor.Reshape(new TensorShape(1,1,outheight, outwidth));
                            Debug.Log($"Changed Output Tensor Shape: {outputTensor.shape}");
                        }

                        Tensor<float> cpuoutput=NormalizeTensor(outputTensor);
                        // Tensor を RenderTexture に変換
                        outputRendertexture = TextureConverter.ToTexture(cpuoutput);
                        outputRendertexture=ResizeRenderTexture(outputRendertexture, inputTexture.width, inputTexture.height);
                        this.mat.mainTexture= outputRendertexture;
                        cpuoutput.Dispose();
                    }
                    else
                    {
                        Debug.LogError("Output tensor is null.");
                    }
                }
            }
        }
        catch (Exception e)
        {
            Debug.LogError($"Error during model processing: {e.Message}");
        }
        finally
        {
            Profiler.EndSample();
        }

        return outputRendertexture;

    }

    public void Release()
    {
        m_Worker.Dispose();
    }

}