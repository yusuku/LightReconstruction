using System.Collections.Generic;
using System.Runtime.InteropServices;
using UnityEngine;



public class CPURunEstimation : MonoBehaviour
{
    public ComputeShader cs;
    public Texture LDRtex;
    public Transform parent;
    
    LightEstimation LightsEnv;
    void Start()
    {
        LightsEnv=new LightEstimation(cs,LDRtex,parent);
    }
    // Update is called once per frame
    void Update()
    {   
        LightsEnv.ReconstructLights();
    }   
}
public class LightEstimation
{
    ComputeShader cs;
    Texture LDRtex;
    List<GameObject> Lights;
    Transform parent;
    int width, height;
    Vector4[] HDRtex;
    float[] Luminances;
    int[] labels;
    float Yt;
    int lightcount;

    Vector4[] Irradiance; Vector3[] Els;
    Vector3[] Positions;


    public LightEstimation(ComputeShader cs, Texture LDRtex, Transform parent)
    {
        this.cs = cs;
        this.LDRtex = LDRtex;
        this.width = LDRtex.width; this.height = LDRtex.height;
        this.parent = parent;
        this.Lights = new List<GameObject>();


        this.HDRtex = new Vector4[width * height];
    }


    public (Vector3[], Color[], float[]) estimation()
    {
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        InverseToneMapping();
        (Luminances, Yt) = SetThresholdingLuminances();
        (labels, lightcount) = BreathfirstSearch(Luminances, Yt);
        (Irradiance, Els) = IrradianceSetting(labels, lightcount, HDRtex, Yt, Luminances);
        Positions = LightPosition(Irradiance, lightcount, labels, Els);

        Color[] colors = new Color[Els.Length];
        float[] intensities = new float[Els.Length];
        for (int i = 0; i < Els.Length; i++)
        {
            colors[i] = new Color(Els[i].x, Els[i].y, Els[i].z, 1);
            intensities[i] = Vector3.Dot(Els[i], new Vector3(lr, lg, lb));
        }
        return (Positions, colors, intensities);
    }

    public void ReconstructLights()
    {
        int HightLight = 0;
        (Vector3[] positions, Color[] colors, float[] intensities) = this.estimation();
        for (int i = 0; i < positions.Length; i++)
        {
            if (intensities[i] > 0.4f)
            {
                HightLight++;
                if (this.Lights.Count < HightLight)
                {
                    this.Lights.Add(CreateLight(positions[i], colors[i], intensities[i], parent));
                }
                else
                {
                    UpdateLight(this.Lights[HightLight-1], positions[i], colors[i], intensities[i]);
                }
            }
        }
        Debug.Log(HightLight);

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


    public void InverseToneMapping()
    {

        ComputeBuffer HDRtexBuffer = new ComputeBuffer(width * height, Marshal.SizeOf<Vector4>());
        int id = this.cs.FindKernel("LDR2HDR");
        uint threadSizeX, threadSizeY, threadSizeZ;
        cs.GetKernelThreadGroupSizes(id, out threadSizeX, out threadSizeY, out threadSizeZ);
        int groupcountX = width / (int)threadSizeX;
        int groupcountY = height / (int)threadSizeY;
        int groupcountZ = 1;


        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        cs.SetInt("width", width); cs.SetInt("height", height);
        cs.SetFloat("lr", lr); cs.SetFloat("lg", lg); cs.SetFloat("lb", lb);
        cs.SetTexture(id, "LDR2HDR_LDR", LDRtex);
        cs.SetBuffer(id, "HDRtexBuffer", HDRtexBuffer);

        cs.Dispatch(id, groupcountX, groupcountY, groupcountZ);
        HDRtexBuffer.GetData(this.HDRtex);
        HDRtexBuffer.Release();

    }
    public (float[], float) SetThresholdingLuminances()
    {
        int width = this.width, height = this.height;
        float[] Luminances = new float[width * height];
        float mean = 0, mean2 = 0;
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;
                float Yp = HDRtex[idx].x * lr + HDRtex[idx].y * lg + HDRtex[idx].z * lb;
                Luminances[idx] = Yp;
                mean += Yp;
                mean2 += Yp * Yp;

            }
        }
        mean /= width * height; mean2 /= width * height;
        float sigma = Mathf.Sqrt(mean2 - (mean * mean));

        float Yt = mean + 2 * sigma;
        return (Luminances, Yt);
    }

    (int[], int) BreathfirstSearch(float[] Luminances, float Yt)
    {
        int width = this.width, height = this.height;
        int[] labels = new int[width * height];
        int LightCount = LabelBreadthFirstSerch(Luminances, labels, width, height, Yt);

        return (labels, LightCount);
    }
    int LabelBreadthFirstSerch(float[] InputTex, int[] labels, int width, int height, float LuminanceThreshold)
    {
        int componentCount = 0;
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (labels[x + y * width] == 0 && IsAboveThreshold(InputTex[x + y * width], LuminanceThreshold))
                {
                    componentCount++;
                    int count = BFS(x, y, componentCount, InputTex, labels, width, height, LuminanceThreshold);
                }
            }
        }
        return componentCount;
    }

    bool IsAboveThreshold(float luminance, float LuminanceThreshold)
    {
        return luminance >= LuminanceThreshold;
    }

    int BFS(int startX, int startY, int label, float[] pixels, int[] labels, int width, int height, float LuminanceThreshold)
    {
        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(new Vector2Int(startX, startY));
        labels[startX + startY * width] = label;
        int count = 1;

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int x = current.x;
            int y = current.y;

            for (int i = 0; i < 4; i++)
            {
                int nx = x + dx[i];
                int ny = y + dy[i];


                if (nx >= 0 && nx < width && ny >= 0 && ny < height && labels[nx + ny * width] == 0 && IsAboveThreshold(pixels[nx + ny * width], LuminanceThreshold))
                {
                    queue.Enqueue(new Vector2Int(nx, ny));
                    labels[nx + ny * width] = label;
                    count++;
                }
            }
        }
        return count;
    }

    (Vector4[] Irradiance, Vector3[] Els) IrradianceSetting(int[] labels, int LightCount, Vector4[] HDRtex, float Yt, float[] Luminances)
    {
        int width = this.width, height = this.height;
        Vector4[] Irradiances = new Vector4[width * height];
        Vector3[] Els = new Vector3[LightCount + 1];
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;

                if (labels[idx] != 0)
                {
                    float OmegaP = GetPixelSolidAngle(x, y);
                    Vector3 HDR = new Vector3(HDRtex[idx].x, HDRtex[idx].y, HDRtex[idx].z);
                    Vector3 Ep = OmegaP * (HDR - HDR * Mathf.Min(1, Yt / Luminances[idx]));
                    Irradiances[idx] = new Vector4(Ep.x, Ep.y, Ep.z, labels[idx]);

                    Els[labels[idx]] += Ep;
                }
            }
        }

        return (Irradiances, Els);

    }
    public float GetPixelSolidAngle(int x, int y)
    {
        // Convert pixel (x, y) to spherical coordinates (theta1, theta2, dPhi)
        var (theta1, theta2, dPhi) = PixelToSpherical(x, y);

        // Calculate and return the solid angle
        return CalculateSolidAngle(theta1, theta2, dPhi);
    }
    public (float theta1, float theta2, float dPhi) PixelToSpherical(int x, int y)
    {
        // Convert pixel x, y to phi (longitude) and theta (latitude) in radians
        float dPhi = Mathf.Deg2Rad * (360f / this.width);
        float thetaMid = Mathf.Deg2Rad * (180f - 180f * (y / (float)this.height));
        float dTheta = Mathf.Deg2Rad * (180f / this.height);

        float theta1 = thetaMid - dTheta / 2;
        float theta2 = thetaMid + dTheta / 2;

        return (theta1, theta2, dPhi);
    }
    public float CalculateSolidAngle(float theta1, float theta2, float dPhi)
    {
        return (Mathf.Cos(theta1) - Mathf.Cos(theta2)) * dPhi;
    }
    Vector3[] LightPosition(Vector4[] Irradiances, int LightCount, int[] labels, Vector3[] Els)
    {
        float lr = 0.3f, lg = 0.59f, lb = 0.11f;
        Vector2[] PolarPosion = new Vector2[LightCount + 1];// Polar 0:None, 1-LightCount: Light
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                int idx = x + y * width;


                if (labels[idx] != 0)
                {
                    float YEp = Irradiances[idx].x * lr + Irradiances[idx].y * lg + Irradiances[idx].z * lb;
                    Vector2 PixelPolar = XY2Polar(x, y, width, height);

                    PolarPosion[labels[idx]] += YEp * PixelPolar;

                }
            }
        }

        for (int i = 1; i <= LightCount; i++)
        {
            float YEl = Els[i].x * lr + Els[i].y * lg + Els[i].z * lb;
            PolarPosion[i] /= YEl;
        }
        Vector3[] Position3D = new Vector3[LightCount + 1];
        for (int i = 0; i < PolarPosion.Length; i++)
        {
            Position3D[i] = PolarToCartesian(PolarPosion[i].x, PolarPosion[i].y, 1);
        }
        return Position3D;

    }
    Vector2 XY2Polar(int x, int y, int width, int height)
    {
        Vector2 polar;

        polar = new Vector2((1 - x / (float)width) * 2 * Mathf.PI - Mathf.PI, Mathf.PI - y / (float)height * Mathf.PI);

        return polar;
    }
    public static Vector3 PolarToCartesian(float phi, float theta, float radius = 1.0f)
    {
        float x = radius * Mathf.Sin(theta) * Mathf.Cos(phi);
        float y = radius * Mathf.Cos(theta);
        float z = radius * Mathf.Sin(theta) * Mathf.Sin(phi);

        return new Vector3(x, y, z);
    }
}
