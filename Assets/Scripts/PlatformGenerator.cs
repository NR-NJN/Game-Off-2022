using System.Collections.Generic;
using UnityEngine;

public class PlatformGenerator : MonoBehaviour
{
    public GameObject player;
    public GameObject firstPlatform;
    public GameObject[] platforms;
    public GameObject[] scenery;
    public int maxPlatforms = 7;
    public float spawnDistance = 70f;
    public int sceneryPerPlatform = 2;
    public Vector2 sceneryOffset = new Vector2(2f, 3f);

    private readonly Queue<GameObject> spawned = new Queue<GameObject>();
    private Transform lastEnd;
    private int platformCount;

    void Start()
    {
        spawned.Enqueue(firstPlatform);
        lastEnd = firstPlatform.transform.Find("end");
    }

    void Update()
    {
        while (lastEnd.position.x < player.transform.position.x + spawnDistance)
            Spawn();
    }

    private void Spawn()
    {
        GameObject platform = Instantiate(platforms[Random.Range(0, platforms.Length)]);
        Transform start = platform.transform.Find("start");
        platform.transform.position += lastEnd.position - start.position;
        platform.name = "road_" + ++platformCount;

        AddScenery(platform.transform, start.position, platform.transform.Find("end").position);
        lastEnd = platform.transform.Find("end");

        spawned.Enqueue(platform);
        while (spawned.Count > maxPlatforms)
            Destroy(spawned.Dequeue());
    }

    private void AddScenery(Transform platform, Vector3 from, Vector3 to)
    {
        if (scenery.Length == 0)
            return;

        for (int i = 0; i < sceneryPerPlatform; i++)
        {
            Vector3 position = Vector3.Lerp(from, to, Random.value) + Vector3.forward * Random.Range(sceneryOffset.x, sceneryOffset.y);
            Quaternion rotation = Quaternion.Euler(0f, Random.Range(0f, 360f), 0f);
            Instantiate(scenery[Random.Range(0, scenery.Length)], position, rotation, platform);
        }
    }
}
