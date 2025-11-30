using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class PlayerG1 : MonoBehaviour
{
    public float fireRate;

    public GameObject viewFinder;

    private float _curFireRate;

    private GameObject _viewFinderClone;

    private bool _isShooted;

    private void Awake()
    {
        _curFireRate = fireRate;
    }

    private void Start()
    {
        if (viewFinder)
        {
            _viewFinderClone = Instantiate(viewFinder, Vector3.zero, Quaternion.identity);
        }
    }

    private void Update()
    {
        Vector3 mousePos = Camera.main.ScreenToWorldPoint(Input.mousePosition);

        if (Input.GetMouseButtonDown(0) && !_isShooted)
        {
            Shoot(mousePos);
        }

        if (_isShooted)
        {
            _curFireRate -= Time.deltaTime;

            if (_curFireRate <= 0)
            {
                _isShooted = false;

                _curFireRate = fireRate;
            }

            GameGUIManagerG1.Ins.UpdateFireRate(_curFireRate / fireRate);
        }

        if (_viewFinderClone)
            _viewFinderClone.transform.position = new Vector3(mousePos.x, mousePos.y, 0);
    }

    private void Shoot(Vector3 mousePos)
    {
        _isShooted = true;

        Vector3 shootDir = Camera.main.transform.position - mousePos;

        shootDir.Normalize();

        RaycastHit2D[] hits = Physics2D.RaycastAll(mousePos, shootDir);

        if (hits != null && hits.Length > 0)
        {
            for (int i = 0; i < hits.Length; i++)
            {
                RaycastHit2D hit = hits[i];

                if (hit.collider && (Vector3.Distance((Vector2)hit.collider.transform.position, (Vector2)mousePos) <= 0.4f))
                {
                    Bird bird = hit.collider.GetComponent<Bird>();

                    if (bird)
                    {
                        bird.Die();
                    }
                }
            }
        }

        CineController.Ins.ShakeTrigger();

        AudioControllerG1.Ins.PlaySound(AudioControllerG1.Ins.shooting);
    }
}
