using System;
using System.Collections.Generic;
using Unity.Sentis;
using UnityEngine;
using UnityEngine.UIElements;
using static TMPro.SpriteAssetUtilities.TexturePacker_JsonArray;

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

    Vector3 top_cameraposition;
    Vector3 bottom_cameraposition;
    int width,height;

    List<GameObject> Lights=new List<GameObject>();
    // Start is called once before the first execution of Update after the MonoBehaviour is created
    void Start()
    {
        top_cameraposition=new Vector3 (0,1,0);
        bottom_cameraposition=new Vector3 (0,0,0);
        width = TopCamera.width; height= TopCamera.height;
        midas = new MidasEstimation(modelAsset, Debugmat);
        ToplightEstimation = new LightEstimation(LitEstics, TopCamera, parent);
        Bottom_texDepthGPUInstancing=new TextureDepthGPUInstancing(instance_mat,instance_mesh, instance_computeShader, BottomCamera);
    }

    // Update is called once per frame
    void Update()
    {
        TopDepth=midas.inference(TopCamera);
        BottomDepth=midas.inference(BottomCamera);

        (Vector3[] Toppositions, Color[] colors, float[] intensities)=ToplightEstimation.estimation();
        Vector3[] LitPositions=GetLightPositions(Toppositions, TopDepth, top_cameraposition, bottom_cameraposition);
        ReconstructLights(Lights, LitPositions, colors, intensities);
        Bottom_texDepthGPUInstancing.DrawMeshes(BottomDepth);


    }
    private void OnDestroy()
    {
        Bottom_texDepthGPUInstancing.Release();
        midas.Release();
    }

    public void ReconstructLights(List<GameObject> Lights, Vector3[] LitPositions, Color[] colors, float[] intensities)
    {
        int HightLight = 0;
        for (int i = 0; i < LitPositions.Length; i++)
        {
            if (intensities[i] > 0.1f)
            {
                HightLight++;
                if (Lights.Count < HightLight)
                {
                    Lights.Add(CreateLight(LitPositions[i], colors[i], intensities[i], parent));
                }
                else
                {
                    UpdateLight(Lights[HightLight - 1], LitPositions[i], colors[i], intensities[i]);
                }
            }
        }
        Debug.Log("HightLight Count: " + HightLight);
        Debug.Log("lights count:" + LitPositions.Length);

    }
    GameObject CreateLight(Vector3 position, Color color, float intensity, Transform parent)
    {
        // 新しいGameObjectを作成
        GameObject lightObject = new GameObject("DirectionalLight");

        // Transformの設定
        lightObject.transform.position = position;
        if (parent != null)
        {
            lightObject.transform.parent = parent;
        }

        // Lightコンポーネントを追加
        Light lightComponent = lightObject.AddComponent<Light>();

        // Lightの設定
        lightComponent.type = UnityEngine.LightType.Directional; // ライトの種類を設定
        lightComponent.color = color;               // ライトの色を設定
        lightComponent.intensity = intensity;       // ライトの強度を設定
        lightComponent.shadows = LightShadows.Soft; // シャドウを設定

        // ライトの方向を設定
        lightObject.transform.LookAt(Vector3.zero);

        return lightObject;
    }

    void UpdateLight(GameObject light, Vector3 position, Color color, float intensity)
    {
        Light lightComponent = light.GetComponent<Light>();
        // Lightの設定
        lightComponent.type = UnityEngine.LightType.Directional; // ライトの種類を設定
        lightComponent.color = color;               // ライトの色を設定
        lightComponent.intensity = intensity;       // ライトの強度を設定
        lightComponent.shadows = LightShadows.Soft; // シャドウを設定

        // ライトの方向を設定
        light.transform.LookAt(Vector3.zero);
    }

    public Vector3[] GetLightPositions(Vector3[] litpositions,Texture depthtex, Vector3 top_cameraposition, Vector3 bottom_cameraposition)
    {
        int width=depthtex.width;
        int height=depthtex.height;
       Vector3[] LitPositions=new Vector3[litpositions.Length];
        for (int i = 0; i < litpositions.Length; i++)
        {
            Vector2 Polar=CartesianToPolar(litpositions[i], width, height);
            Vector2 Pixel = PolarToPixel(Polar, width, height);
            float Pixeldeph=PixelDepths(depthtex, Pixel);
            Vector3 TopLitposition=PolarToCartesian(Polar, Pixeldeph);
            Vector3 Litposition = TopLitposition + (top_cameraposition - bottom_cameraposition);
            LitPositions[i]=Litposition;
        }
        return LitPositions;
    }

    public static float PixelDepths(Texture depthTexture, Vector2 pixel)
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
            return -1;
        }

        
        return depthTex.GetPixel((int)Mathf.Round(pixel.x), (int)Mathf.Round(pixel.y)).r; // Assuming depth is stored in the red channel
        
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

    public static Vector2 CartesianToPolar(Vector3 cartesian, int width, int height)
    {
        // Extract the spherical coordinates from the Cartesian coordinates
        float radius = cartesian.magnitude;
        float theta = Mathf.Acos(cartesian.y / radius); // Inclination
        float phi = Mathf.Atan2(cartesian.z, cartesian.x); // Azimuth
        return new Vector2(phi, theta);
    }
    public Vector2 PolarToPixel(Vector2 Polar, int width, int height)
    {
        float phi = Polar.x;float theta = Polar.y;
        // Convert polar coordinates to pixel coordinates
        int x = (int)((1 - (phi + Mathf.PI) / (2 * Mathf.PI)) * width);
        int y = (int)((Mathf.PI - theta) / Mathf.PI * height);
        return new Vector2(x, y);
    }
    public Vector3 PolarToCartesian(Vector2 Polar,float depth)
    {
        float theta = Polar.y;float phi=Polar.x;
        float r = depth;
        float x = r * Mathf.Sin(theta) * Mathf.Cos(phi);
        float y = r * Mathf.Cos(theta);
        float z = r * Mathf.Sin(theta) * Mathf.Sin(phi);
        return new Vector3(x, y, z);
    }

}
