using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Loader : MonoBehaviour
{
    public GameObject skidManagerPrefab;

    public static Loader instance;

    void Awake()
    {
        //Singleton Setup
        if (instance == null)
        {
            instance = this;
        }
        else
        {
            Destroy(gameObject);
        }

        DontDestroyOnLoad(gameObject);
    }

    // Start is called before the first frame update
    void Start()
    {
        if (Skidmarks.instance == null)
        {
            Instantiate(skidManagerPrefab);
        }
    }

    // Update is called once per frame
    void Update()
    {

    }
}
