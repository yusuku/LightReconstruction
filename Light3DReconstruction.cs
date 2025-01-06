using Unity.Sentis;
using UnityEngine;

public class Light3DReconstruction : MonoBehaviour
{
    Texture TopCamera;
    Texture BottomCamera;

    MidasEstimation midas;
    ModelAsset modelAsset;
    Material Debugmat;

    LightEstimation ToplightEstimation;
    ComputeShader LitEstics;
    Transform parent;

    TextureDepthGPUInstancing Top_texDepthGPUInstancing;
    TextureDepthGPUInstancing Bottom_texDepthGPUInstancing;
    Material instance_mat;
    Mesh instance_mesh;
    ComputeShader instance_computeShader;

    Texture TopDepth;
    Texture BottomDepth;


    int width,height;
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        width= TopCamera.width; height= TopCamera.height;
        midas = new MidasEstimation(modelAsset, Debugmat);
        ToplightEstimation = new LightEstimation(LitEstics, TopCamera, parent);
        Bottom_texDepthGPUInstancing=new TextureDepthGPUInstancing(instance_mat,instance_mesh, instance_computeShader, BottomCamera);
    }

    // Update is called once per frame
    void Update()
    {
        TopDepth=midas.inference(TopCamera);
        BottomDepth=midas.inference(BottomCamera);

        (Vector3[] Toppositions,_,_)=ToplightEstimation.estimation();
        Vector2[] TopPixelpositions=CartesianToPixel(Toppositions, width,height);
        float[] TopLightsDepth=PixelDepths(TopDepth, TopPixelpositions);



    }


    public static float[] PixelDepths(Texture depthTexture, Vector2[] positions)
    {
        Texture2D depthTex;

        // Convert texture to Texture2D
        if (depthTexture is Texture2D)
        {
            depthTex = depthTexture as Texture2D;
        }
        else if (depthTexture is RenderTexture)
        {
            depthTex = ConvertRenderTextureToTexture2D(depthTexture as RenderTexture);
        }
        else
        {
            Debug.LogError("Invalid Texture type. Must be Texture2D or RenderTexture.");
            return null;
        }

        float[] depths = new float[positions.Length];

        // Get depth values for each position
        for (int i = 0; i < positions.Length; i++)
        {
            int x = Mathf.Clamp((int)positions[i].x, 0, depthTex.width - 1);
            int y = Mathf.Clamp((int)positions[i].y, 0, depthTex.height - 1);
            depths[i] = depthTex.GetPixel(x, y).r; // Assuming depth is stored in the red channel
        }

        return depths;
    }

    public static Texture2D ConvertRenderTextureToTexture2D(RenderTexture renderTexture)
    {
        // アクティブな RenderTexture をバックアップ
        RenderTexture currentActiveRT = RenderTexture.active;

        // 指定された RenderTexture をアクティブに設定
        RenderTexture.active = renderTexture;

        // Texture2D を作成
        Texture2D texture2D = new Texture2D(renderTexture.width, renderTexture.height, TextureFormat.RGBA32, false);

        // ピクセルデータを読み込み
        texture2D.ReadPixels(new Rect(0, 0, renderTexture.width, renderTexture.height), 0, 0);
        texture2D.Apply();

        // アクティブな RenderTexture を元に戻す
        RenderTexture.active = currentActiveRT;

        return texture2D;
    }

    public static Vector2 CartesianToPixel(Vector3 cartesian, int width, int height)
    {
        // Extract the spherical coordinates from the Cartesian coordinates
        float radius = cartesian.magnitude;
        float theta = Mathf.Acos(cartesian.y / radius); // Inclination
        float phi = Mathf.Atan2(cartesian.z, cartesian.x); // Azimuth

        // Convert polar coordinates to pixel coordinates
        int x = (int)((1 - (phi + Mathf.PI) / (2 * Mathf.PI)) * width);
        int y = (int)((Mathf.PI - theta) / Mathf.PI * height);

        return new Vector2(x, y);
    }
    public static Vector2[] CartesianToPixel(Vector3[] cartesians, int width, int height)
    {
        Vector2[] pixels = new Vector2[cartesians.Length];
        for (int i = 0; i < cartesians.Length; i++)
        {
            pixels[i] = CartesianToPixel(cartesians[i], width, height);
        }
        return pixels;
    }
}
