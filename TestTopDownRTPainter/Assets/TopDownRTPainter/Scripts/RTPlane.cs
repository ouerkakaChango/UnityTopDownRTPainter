using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class RTPlane : MonoBehaviour
{
    public Vector2 size = new Vector2(14.0f,14.0f);
    public bool useDebugRenderer=false;
    [HideInInspector]
    public Renderer debugRenderer;
    public float FPS = 24.0f;
    public float fadeRate = 1.0f;
    float time_update;
    float t_update;

    public Vector2Int resolution = new Vector2Int(128, 128);
    public RenderTexture rt1,rt2;

    RTUnitTag[] allTags;
    ComputeShader cs;
    ComputeBuffer buffer_posArr;
    Vector2[] posArr;
    // Start is called before the first frame update
    void Start()
    {
        time_update = 1.0f / FPS;
        cs = (ComputeShader)Resources.Load("ComputeShaders/TopDownRTPainter");
        InitRT();
        if(useDebugRenderer)
        {
            debugRenderer.material.SetTexture("_MainTex", rt1);
        }
        t_update = 0;

        allTags = (RTUnitTag[])GameObject.FindObjectsOfType(typeof(RTUnitTag));
    }

    // Update is called once per frame
    void Update()
    {
        t_update += Time.deltaTime;
        if(t_update>=time_update)
        {
            FadeAndDrawNewPos();
            t_update -= time_update;
        }
    }

    private void OnDrawGizmos()
    {
        Gizmos.color = Color.green;
        Gizmos.DrawWireCube(transform.position, new Vector3(size.x, 0.01f, size.y));
    }

    void InitRT()
    {
        rt1 = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf);
        rt1.enableRandomWrite = true;
        rt1.Create();
        rt2 = new RenderTexture(resolution.x, resolution.y, 0, RenderTextureFormat.ARGBHalf);
        rt2.enableRandomWrite = true;
        rt2.Create();

        //##################################
        //### compute
        int kInx = cs.FindKernel("InitPaint");

        cs.SetTexture(kInx, "Result", rt1);

        //cs.SetInt("SPP", SPP);
        //cs.SetFloat("darkPower", darkPower);
        //cs.SetBuffer(kInx, "tris", buffer_tris);
        //cs.SetVector("lightDir", targetLight.transform.forward);

        cs.Dispatch(kInx, resolution.x / 8, resolution.y / 8, 1);
        //### compute
        //#####################################;

        //##################################
        //### compute
        kInx = cs.FindKernel("InitPaint");

        cs.SetTexture(kInx, "Result", rt2);
        cs.Dispatch(kInx, resolution.x / 8, resolution.y / 8, 1);
        //### compute
        //#####################################;
    }

    void FadeAndDrawNewPos()
    {
        Debug.Log("update Draw");
        float copyAlpha = (1.0f - time_update) * fadeRate;
        //##################################
        //### compute
        int kInx = cs.FindKernel("FadeCopy");

        cs.SetTexture(kInx, "Result", rt2);
        cs.SetTexture(kInx, "LastFrame", rt1);
        cs.SetFloat("copyAlpha", copyAlpha);

        cs.Dispatch(kInx, resolution.x / 8, resolution.y / 8, 1);
        //### compute
        //#####################################;

        UpdatePosArr();
        Debug.Log(posArr[0]);
        Debug.Log(posArr[1]);
        PreComputeBuffer(ref buffer_posArr, sizeof(float) * 2, posArr);
        //##################################
        //### compute
        kInx = cs.FindKernel("PaintPos");

        cs.SetTexture(kInx, "Result", rt1);
        cs.SetTexture(kInx, "LastFrame", rt2);
        cs.SetBuffer(kInx, "posArr", buffer_posArr);
        cs.SetFloat("radius", 2*0.5f/size.x);

        cs.Dispatch(kInx, resolution.x / 8, resolution.y / 8, 1);
        //### compute
        //#####################################;
        buffer_posArr.Dispose();
    }

    List<Vector2> tempPosList = new List<Vector2>();
    void UpdatePosArr()
    {
        tempPosList.Clear();
        for (int i=0;i<allTags.Length;i++)
        {
            if( allTags[i]!=null && allTags[i].enabled && allTags[i].gameObject.activeInHierarchy)
            {
                tempPosList.Add(ConvertToUVSpace(allTags[i].transform.position));
            }
        }
        posArr = tempPosList.ToArray();
    }

    Vector2 ConvertToUVSpace(Vector3 worldPos)
    {
        Vector2 center = VecXZ(transform.position);
        Vector2 pos = VecXZ(worldPos);
        return (pos - center) / size + Vector2.one*0.5f;
    }

    //如果是元素个数一直在变动的情况，还不如就每帧new ComputeBuffer，反正也不是很费好像
    static public void PreComputeBuffer(ref ComputeBuffer buffer, int stride, in System.Array dataArr)
    {
        buffer = new ComputeBuffer(dataArr.Length, stride);
        buffer.SetData(dataArr);
    }

    static Vector2 VecXZ(Vector3 v)
    {
        return new Vector2(v.x, v.z);
    }
}