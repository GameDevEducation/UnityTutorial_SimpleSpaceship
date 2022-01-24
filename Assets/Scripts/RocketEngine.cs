using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.VFX;

public class RocketEngine : MonoBehaviour
{
    [SerializeField] VisualEffect LinkedEffect;
    [SerializeField] AudioSource LinkedSource;
    [SerializeField] AudioClip EngineSound;
    [SerializeField] [Range(0f, 1f)] float MaxVolume = 1f;

    float _Thrust = 0f;
    public float Thrust
    {
        get
        {
            return _Thrust;
        }
        set
        {
            if (value != _Thrust)
            {
                _Thrust = value;
                LinkedSource.volume = _Thrust * MaxVolume;
                LinkedEffect.SetFloat("Thrust", _Thrust);
            }
        }
    }

    // Start is called before the first frame update
    void Start()
    {
        LinkedSource.volume = 0;
        _Thrust = 0f;
        LinkedEffect.SetFloat("Thrust", 0);

        LinkedSource.clip = EngineSound;
        LinkedSource.loop = true;
        LinkedSource.Play();
    }

    // Update is called once per frame
    void Update()
    {
        
    }
}
