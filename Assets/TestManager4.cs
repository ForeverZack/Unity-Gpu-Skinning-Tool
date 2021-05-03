using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Framework.GpuSkinning;

public class TestManager4 : MonoBehaviour
{
    public GameObject modifyModelMatrixPrefab;
    public string animationName;
    public int interval;

    List<Vector3> positions = new List<Vector3>();
    // Start is called before the first frame update
    void Start()
    {
        for (int i = -5; i < 5; ++i)
        {
            for (int j=-5; j<5;++j)
            {
                positions.Add(new Vector3(i* interval, 0, j* interval));
            }
        }

        StartCoroutine(loadPrefab());
    }

    int counter = 0;
    IEnumerator loadPrefab()
    {
        while(counter<99)
        {
            GameObject gameObject = GameObject.Instantiate(modifyModelMatrixPrefab, positions[counter], Quaternion.identity);
            GPUSkinningAnimator animator = gameObject.GetComponent<GPUSkinningAnimator>();
            animator.PlayAnimation(animationName, true);

            ++counter;

            yield return new WaitForSeconds(0.2f);
        }



        yield return null;
    }
    

    // Update is called once per frame
    void Update()
    {
        
    }
}
