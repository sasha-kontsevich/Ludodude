using UnityEngine;
using System.Collections.Generic;

public class SpawnFromPoints : MonoBehaviour
{
    public GameObject[] prefabs; 
    public int spawnCount = 3;        

    void Start()
    {
        List<Transform> pointsList = new List<Transform>();
        foreach (Transform child in transform)
        {
            pointsList.Add(child);
        }

        for (int i = 0; i < spawnCount && pointsList.Count > 0; i++)
        {
            int randomPointIndex = Random.Range(0, pointsList.Count);
            Transform chosenPoint = pointsList[randomPointIndex];


            int randomCircleIndex = Random.Range(0, prefabs.Length);
            GameObject chosen = prefabs[randomCircleIndex];

            Instantiate(chosen, chosenPoint.position, Quaternion.identity);

            pointsList.RemoveAt(randomPointIndex); 
        }
    }


}