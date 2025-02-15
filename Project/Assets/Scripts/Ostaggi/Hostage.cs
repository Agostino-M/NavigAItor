using UnityEngine;

public class Hostage : MonoBehaviour
{
    public int Priority;


    void Start()
    {
        Priority = Random.Range(1, 10);
    }
}