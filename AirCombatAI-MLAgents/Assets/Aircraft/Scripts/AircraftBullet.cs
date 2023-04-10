using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;


namespace Aircraft
{
    public class AircraftBullet : MonoBehaviour
    {
        [Tooltip("子弹速度")] public float speed = 1;

        [Tooltip("子弹有效时间")] public float lifetime = 7;

        [Tooltip("发射的战机")]
        public AircraftAgent fireAgent { get; internal set; }
        
        private void Start()
        {
            Invoke("SelfDestroy", lifetime);
        }
        
        // Update is called once per frame
        void Update()
        {
            transform.Translate(0, 0, speed * 1000 * Time.deltaTime, Space.Self);
        }
        
        private void OnTriggerEnter(Collider other)
        {
            // 撞击到别人，销毁自己
            if (other.transform.CompareTag("Agent"))
            {
                // 撞击到战机，销毁逻辑，之后再处理
                AircraftAgent agent = other.gameObject.GetComponent<AircraftAgent>();
                if (agent.IsDestroyed || agent == fireAgent)
                {
                    // 这个Agent已经被摧毁了或是发射的战机，就直接穿过去不处理
                    return;
                }
                // 击中敌机
                fireAgent.OnBulletHit(agent);
            }

            StartCoroutine(DestroyAfterOneFrame());
        }
        
        
        private IEnumerator DestroyAfterOneFrame()
        {
            yield return null; // 等待一帧
            Destroy(gameObject); // 销毁物体
        }
        
        private void SelfDestroy()
        {
            Destroy(this.gameObject);
        }
    }
}