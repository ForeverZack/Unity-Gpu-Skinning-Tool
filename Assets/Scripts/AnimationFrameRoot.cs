using System.Collections;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

public class AnimationFrameRoot : MonoBehaviour
{
    public Material instanceMaterial;
    public float perPixelWorldSize = 0.25f;

    public Vector2Int _FrameIndexTextureSize;
    Texture2D _FrameIndexTexture;

    public int maxRandomRange = 50;

    // Start is called before the first frame update
    void Start()
    {
        _FrameIndexTexture = new Texture2D(_FrameIndexTextureSize.x, _FrameIndexTextureSize.y, TextureFormat.R8, false);
        _FrameIndexTexture.filterMode = FilterMode.Point;
        _FrameIndexTexture.wrapMode = TextureWrapMode.Repeat;
        for (int x=0; x< _FrameIndexTextureSize.x; ++x)
        {
            for (int y=0; y<_FrameIndexTextureSize.y; ++y)
            {
                Color color = new Color(Random.Range(0, maxRandomRange) / 255.0f, 0, 0, 0);
                _FrameIndexTexture.SetPixel(x, y, color);
            }
        }
        _FrameIndexTexture.Apply();

        instanceMaterial.SetTexture("_FrameIndexTex", _FrameIndexTexture);
        instanceMaterial.SetFloat("_PerPixelWorldSize", perPixelWorldSize);
    }

    // Update is called once per frame
    void Update()
    {
        instanceMaterial.SetMatrix("_WorldToAnimRootNodeMatrix", transform.worldToLocalMatrix);
    }

}
