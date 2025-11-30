using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Bird : MonoBehaviour
{
    public float xSpeed;
    public float minYspeed;
    public float maxYspeed;

    public GameObject deathVfx;

    private Rigidbody2D _rb;

    private bool _moveLeftOnStart;
    private bool _isDead;

    private void Start()
    {
        RandomMovingDirection();
    }

    private void Awake()
    {
        _rb = GetComponent<Rigidbody2D>();
    }

    private void Update()
    {
        _rb.linearVelocity = _moveLeftOnStart ? new Vector2(-xSpeed, Random.Range(minYspeed, maxYspeed))
            : new Vector2(xSpeed, Random.Range(minYspeed, maxYspeed));

        Flip();
    }

    public void RandomMovingDirection()
    {
        _moveLeftOnStart = transform.position.x > 0 ? true : false;
    }

    private void Flip()
    {
        if (_moveLeftOnStart)
        {
            if (transform.localScale.x < 0)
                return;

            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        }
        else
        {
            if (transform.localScale.x > 0)
                return;

            transform.localScale = new Vector3(transform.localScale.x * -1, transform.localScale.y, transform.localScale.z);
        }
    }

    public void Die()
    {
        _isDead = true;

        GameManagerG1.Ins.BirdKilled++;

        Destroy(gameObject);

        if (deathVfx)
            Instantiate(deathVfx, transform.position, Quaternion.identity);

        GameGUIManagerG1.Ins.UpdateKilledCounting(GameManagerG1.Ins.BirdKilled);

    }
}
