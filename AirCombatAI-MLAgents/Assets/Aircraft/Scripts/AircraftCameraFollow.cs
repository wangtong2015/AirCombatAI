using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class AircraftCameraFollow : MonoBehaviour
{
    [Tooltip("相机需要跟随的对象")]
    public Transform target; // 相机需要跟随的对象
    
    // private Vector3 offset; // 相机对物体的偏移（相对位置）
    //
    // // Start is called before the first frame update
    // void Start()
    // {
    //     // 计算初始时刻的偏移，即相对位置
    //     offset = target.transform.InverseTransformPoint(transform.position);
    // }
    
    void LateUpdate()
    {
        // Vector3 desiredPosition = target.transform.TransformPoint(offset);
        // transform.position = desiredPosition; // 更新相机位置
        // transform.LookAt(target);
        transform.position = target.position;
        transform.rotation = target.rotation;
    }
    
}

