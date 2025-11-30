using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleHolder : SingletonG2<CircleHolder>
{
    private float _rotSpeed;
    private float _rotZ;
    private bool _canRot;

    protected override void Awake()
    {
        MakeSingleton(false);
    }

    public void Rotate(float speed)
    {
        _rotSpeed = speed;
        _canRot = true;
    }

    public void StopRotate()
    {
        _canRot = false;
    }

    private void Update()
    {
        if (!_canRot)
            return;

        _rotZ -= _rotSpeed * Time.deltaTime;
        transform.rotation = Quaternion.Euler(0f, 0f, _rotZ);
    }
}
