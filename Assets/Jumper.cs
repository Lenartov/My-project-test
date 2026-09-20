using NUnit.Framework;
using System;
using UnityEngine;
using UnityEngine.Events;

public class Jumper : MonoBehaviour
{
    public float jumpForce = 10f;

    private Rigidbody rb;

    public Action action;

    private void Start()
    {
        rb = GetComponent<Rigidbody>();
    }

    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space))
        {
            Jump();
        }
    }

    private void Jump()
    {
        rb.AddForce(Vector3.up * jumpForce, ForceMode.Impulse);
        action?.Invoke();
    }
}
