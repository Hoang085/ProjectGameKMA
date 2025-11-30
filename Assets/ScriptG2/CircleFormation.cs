using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class CircleFormation : SingletonG2<CircleFormation>
{
    public CircleItem circleItemPb;
    public Transform root;

    private List<CircleItem> m_elements;

    public List<CircleItem> Elements { get => m_elements; set => m_elements = value; }

    protected override void Awake()
    {
        MakeSingleton(false);
        m_elements = new List<CircleItem>();
    }

    public void Draw(int number)
    {
        // Set a targetPosition variable of where to spawn objects.
        Vector3 targetPosition = new Vector3(-0.5f, -1.5f, 0f);

        // Loop through the number of points in the circle.
        for (int i = 0; i < number; i++)
        {
            // Instantiate the prefab.
            CircleItem circleItemClone = Instantiate(circleItemPb);

            // Get the angle of the current index being instantiated
            // from the center of the circle.
            float angle = i * (2 * 3.14159f / number);

            // Get the X Position of the angle times 1.5f. 1.5f is the radius of the circle.
            float x = Mathf.Cos(angle);
            // Get the Y Position of the angle times 1.5f. 1.5f is the radius of the circle.
            float y = Mathf.Sin(angle);

            circleItemClone.transform.SetParent(root);

            // Set the targetPosition to a new Vector3 with the new variables.
            targetPosition = new Vector3(targetPosition.x + x, targetPosition.y + y, 0);

            // Set the position of the instantiated object to the targetPosition.
            circleItemClone.transform.localPosition = targetPosition;

            circleItemClone.Id = i;

            m_elements.Add(circleItemClone);
        }
    }
}
