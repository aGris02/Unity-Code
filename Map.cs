using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Mirror;

public class Map : NetworkBehaviour
{
    private bool[,,] points;
    public int maxX;
    public int maxY;
    public int maxZ;
    private int xChunks = 2; 
    private int yChunks = 1;
    private int zChunks = 2;
    public GameObject chunkPrefab;
    private List<GameObject> chunks = new List<GameObject>();
    private List<Vector3> chunkOrigin = new List<Vector3>();
    public List<Vector3> spawnPositions = new List<Vector3>();
    public List<Vector3> placeholderPositions = new List<Vector3>();
    public List<Quaternion> spawnRotations = new List<Quaternion> { Quaternion.Euler(0, 45, 0), Quaternion.Euler(0, 225, 0), Quaternion.Euler(0, 135, 0), Quaternion.Euler(0, -45, 0),
                                                Quaternion.Euler(0, 0, 0), Quaternion.Euler(0, 180, 0), Quaternion.Euler(0, 90, 0), Quaternion.Euler(0, -90, 0)};

    //make sure chunks are 16x16x16 so total cubes = 4096
    private Vector3[] verticies = new Vector3[16 * 4096];
    private int[] triangles = new int[15 * 4096];
    private Vector2[] uvs = new Vector2[16 * 4096];
    public int randomNum;
    public int players = 1;

    [ClientRpc] public void RpcSetMap(int currentMap) { SetMap(currentMap); }

    [ClientRpc] public void RpcSetRandNum(int num) { randomNum = num; }

    [ClientRpc] public void RpcSetPlayers(int num) { players = num; }

    public void SetMap(int level)
    {
        DestroyChunks();

        //set map size, chunks, spawn pos, place holder pos, spawn rot, map points
        int[] arrayMaxX = new int[] { 16 + players * 2, 35, 32, 32, 32, 64, 32 + players * 2, 32 };
        int[] arrayMaxY = new int[] { 16              , 15, 16, 24, 24, 8 , 16              , 16 }; 
        int[] arrayMaxZ = new int[] { 16 + players * 2, 35, 32, 32, 32, 64, 32 + players * 2, 32 };
        int i = level - 1;
        maxX = arrayMaxX[i];
        maxY = arrayMaxY[i];
        maxZ = arrayMaxZ[i];
        xChunks = Mathf.CeilToInt(maxX / 16f);
        yChunks = Mathf.CeilToInt(maxY / 16f);
        zChunks = Mathf.CeilToInt(maxZ / 16f);
        points = new bool[maxX, maxY, maxZ];

        //spawn disatnce from edge
        float[] offsets = new float[] { 5 , 7, 4 , 3, 7 , 5, 5 , 1  };
        float[] heights = new float[] { 11, 6, 12, 2, 10, 2, 15, 15 };

        //bottom left, top right, top left, bottom right, bottom center, top center, middle left, middle right
        spawnPositions = new List<Vector3> { new Vector3 (offsets[i], heights[i], offsets[i]), new Vector3(maxX - offsets[i] - 1, heights[i], maxZ - offsets[i] - 1), new Vector3(offsets[i], heights[i], maxZ - offsets[i] - 1), new Vector3(maxX - offsets[i] - 1, heights[i], offsets[i]),
                                                 new Vector3((maxX - 1)/2f, heights[i], offsets[i]), new Vector3((maxX - 1)/2f, heights[i], maxZ - offsets[i] - 1), new Vector3(offsets[i], heights[i], (maxZ - 1)/2f), new Vector3(maxX - offsets[i] - 1, heights[i], (maxZ - 1)/2f)};
        placeholderPositions = new List<Vector3> { new Vector3 (3, maxY + 3, 3), new Vector3(maxX - 4, maxY + 3, maxZ - 4), new Vector3(3, maxY + 3, maxZ - 4), new Vector3(maxX - 4, maxY + 3, 3),
                                                       new Vector3((maxX - 1)/2f, maxY + 3, 3), new Vector3((maxX - 1)/2f, maxY + 3, maxZ - 4), new Vector3(3, maxY + 3, (maxZ - 1)/2f), new Vector3(maxX - 4, maxY + 3, (maxZ - 1)/2f)};

        SetMapPoints(level);
        UpdateChunks(new List<int>());
    }

    public void SetMapPoints(int level)
    {
        Vector3[] sphereCenters = new Vector3[] { new Vector3(3, 9, 3) , new Vector3((maxX - 1)/2f, 9, 3), new Vector3(maxX - 4, 9, 3), new Vector3(3, 9, (maxZ - 1)/2f),
                                                      new Vector3(maxX - 4, 9, (maxZ - 1)/2f), new Vector3(3, 9, maxZ - 4), new Vector3((maxX - 1)/2f, 9, maxZ - 4), new Vector3(maxX - 4, 9, maxZ - 4)};

        if (level == 1) //cube with walls on top
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = true;
                        if (x > 3 && x < maxX - 4 && z > 3 && z < maxZ - 4 && y > maxY - 6) points[x, y, z] = false; //bowl
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }

        else if (level == 2) //colosseum
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;
                        if (x == 1 || y == 1 || z == 1 || y == maxY - 1) points[x, y, z] = true; //outside wall
                        if (y == 5 || y == 9 || y == 13 || ((x % 4 == 1 || z % 4 == 1) && (x < 6 || x > 28 || z < 6 || z > 28))) points[x, y, z] = true; //outer rooms
                        if ((x == 8 || x == 12 || x == 16 || x == 20 || x == 24) && (z == 8 || z == 12 || z == 16 || z == 20 || z == 24)) points[x, y, z] = true; //center pillars
                        if (x > 9 && x < 23 && z > 9 && z < 23) points[x, y, z] = false; //center opening
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }

        else if (level == 3) //space rocks
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;
                        for (int a = 0; a < sphereCenters.Length; a++)
                            if (Vector3.Distance(sphereCenters[a], new Vector3(x, y, z)) <= 3) points[x, y, z] = true; //outer spheres

                        if (Vector3.Distance(new Vector3((maxX - 1) / 2f, 9, (maxZ - 1) / 2f), new Vector3(x, y, z)) <= 6) points[x, y, z] = true; //center sphere
                        if (y == 1) points[x, y, z] = true; //floor
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }

        else if (level == 4) //forest
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;

                        if (y == 1 || ((x % 4 == 1 || x % 4 == 2) && (z % 4 == 1 || z % 4 == 2))) points[x, y, z] = true; //trees
                        if (y > -.03f * (Mathf.Pow(x - (maxX - 1) / 2f, 2) + Mathf.Pow(z - (maxZ - 1) / 2f, 2)) + maxY) points[x, y, z] = false; //cut out upsidedown bowl
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;

                    }

        else if (level == 5) //pro map
        {
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;
                        if (y < 10 && !((x > 10 && x < maxX - 11) || (z > 10 && z < maxZ - 11))) points[x, y, z] = true; //four corner cubes
                        if ((x == 5 || x == 6 || x == maxX - 6 || x == maxX - 7 || z == 5 || z == 6 || z == maxZ - 6 || z == maxZ - 7) && (y == 5 || y == 6)) points[x, y, z] = true; //lines connecting cubes
                        if ((y == 13 || y == 14) && !(x < 5 || x > maxX - 6 || z < 5 || z > maxZ - 6) && !(x > 6 && x < maxX - 7 && z > 6 && z < maxZ - 7)) points[x, y, z] = true; //middle square ring
                        if (x > 14 && x < 17 && z > 14 && z < 17) points[x, y, z] = true; //center pillar
                        if ((x == 5 || x == 6 || x == maxX - 6 || x == maxX - 7) && (z == 5 || z == 6 || z == maxZ - 6 || z == maxZ - 7) && y < 20) points[x, y, z] = true; //outer pillars
                        if ((y == 18 || y == 19) && !(x < 10 || x > maxX - 11 || z < 10 || z > maxZ - 11) && !(x > 11 && x < maxX - 12 && z > 11 && z < maxZ - 12)) points[x, y, z] = true; //top square ring
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }

            spawnPositions = new List<Vector3> { new Vector3 (7, 10, 7), new Vector3(maxX - 8, 10, maxZ - 8), new Vector3(7, 10, maxZ - 8), new Vector3(maxX - 8, 10, 7),
                                                 new Vector3((maxX - 1)/2f, 15, 5.5f), new Vector3((maxX - 1)/2f, 15, maxX - 6.5f), new Vector3(5.5f, 15, (maxZ - 1)/2f), new Vector3(maxX - 6.5f, 15, (maxZ - 1)/2f)};
        }

        else if (level == 6) //huge flat sheet
            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;
                        if (y == 1) points[x, y, z] = true; //floor
                        if ((x == 1 || x == maxX - 2 || x == 30 || x == 31) && (z == 1 || z == maxZ - 2 || z == 30 || z == 31)) points[x, y, z] = true; //pillars for orientation
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }

        else if (level == 7) //buildings
        {

            int[] buildingMaxX = new int[] { 31, 24, 49, 35, 42, 19, 27, 37, 31, 31, 44, 22, 39, 13, 53, 43, 12, 28, 14, 32, 24, 54, 23, 15, 43, 51, 56, 38, 10 };
            int[] buildingMinX = new int[] { 25, 17, 45, 30, 38, 7, 20, 31, 19, 19, 38, 12, 29, 6, 42, 36, 1, 25, 11, 10, 13, 48, 18, 10, 37, 45, 45, 26, 2 };
            int[] buildingMaxZ = new int[] { 17, 41, 33, 57, 50, 52, 43, 15, 32, 32, 25, 55, 40, 18, 41, 27, 58, 40, 42, 27, 12, 12, 18, 27, 11, 27, 52, 47, 37 };
            int[] buildingMinZ = new int[] { 12, 38, 28, 53, 44, 42, 46, 10, 26, 26, 15, 44, 31, 12, 31, 16, 53, 28, 31, 20, 2, 9, 15, 20, 0, 18, 45, 42, 25 };
            int[] buildingMaxY = new int[] { 10, 12, 12, 8, 8, 10, 11, 8, 14, 14, 13, 12, 8, 13, 15, 7, 15, 13, 13, 7, 11, 14, 12, 15, 11, 12, 9, 10, 13 };

            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        points[x, y, z] = false;

                        if (y < 5) points[x, y, z] = true; //ground
                        if (!(Mathf.Min(x, maxX - x - 1) > 8 && Mathf.Min(z, maxZ - z - 1) > 8) && y < 14 && y >= 5)
                            if (Mathf.Sqrt(Mathf.Pow(x - 1, 2) + Mathf.Pow((y - 5) * 2, 2)) <= 5 || Mathf.Sqrt(Mathf.Pow(maxX - 2 - x, 2) + Mathf.Pow((y - 5) * 2, 2)) <= 5 || Mathf.Sqrt(Mathf.Pow(z - 1, 2) + Mathf.Pow((y - 5) * 2, 2)) <= 5 || Mathf.Sqrt(Mathf.Pow(maxZ - 2 - z, 2) + Mathf.Pow((y - 5) * 2, 2)) <= 5)
                                points[x, y, z] = true; //outside hill
                        for (int a = 0; a < buildingMaxX.Length; a++)
                            if (x < buildingMaxX[a] && x > buildingMinX[a] && z < buildingMaxZ[a] && z > buildingMinZ[a] && y <= buildingMaxY[a] && buildingMaxX[a] < maxX && buildingMaxZ[a] < maxZ)
                                points[x, y, z] = true; //buildings
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;

                    }
        }

        else if (level == 8) //random map
        {
            int[] primeNums = new int[] { 7, 11, 13, 17, 19, 23, 29, 31, 37, 41, 43, 47, 53, 59, 61, 67, 71, 73, 79, 83, 89, 97, 101, 103, 107, 109, 113, 127, 131, 137, 139, 149, 151, 157, 163, 167, 173, 179, 181, 191, 193, 197, 199, 211, 223, 227, 229, 233, 239, 241, 251, 257, 263, 269, 271, 277, 281, 283, 293, 307, 311, 313, 317, 331, 337, 347, 349, 353, 359, 367, 373, 379, 383, 389, 397, 401, 409, 419, 421, 431, 433, 439, 443, 449, 457, 461, 463, 467, 479, 487, 491, 499, 503, 509, 521, 523, 541, 547, 557, 563, 569, 571, 577, 587, 593, 599, 601, 607, 613, 617, 619, 631, 641, 643, 647, 653, 659, 661, 673, 677, 683, 691, 701, 709, 719, 727, 733, 739, 743, 751, 757, 761, 769, 773, 787, 797, 809, 811, 821, 823, 827, 829, 839, 853, 857, 859, 863, 877, 881, 883, 887, 907, 911, 919, 929, 937, 941, 947, 953, 967, 971, 977, 983, 991, 997, 1009, 1013, 1019, 1021, 1031, 1033, 1039, 1049, 1051 };
            int[] randomNums = new int[6];
            for (int a = 0; a < primeNums.Length - 7; a += 6) //create boxes based on random seed from server
            {
                for (int b = 0; b < 6; b++)
                {
                    if (b == 2 || b == 3) randomNums[b] = (int)((float)(randomNum % primeNums[a + b]) / primeNums[a + b] * (maxY - 1)) + 1;
                    else randomNums[b] = randomNums[b] = (int)((float)(randomNum % primeNums[a + b]) / primeNums[a + b] * (maxX - 1)) + 1;
                }

                bool fill = (randomNums[0] + primeNums[a]) % 2 == 0;
                for (int x = Mathf.Min(randomNums[0], randomNums[1]); x <= Mathf.Max(randomNums[0], randomNums[1]); x++)
                    for (int y = Mathf.Min(randomNums[2], randomNums[3]); y <= Mathf.Max(randomNums[2], randomNums[3]); y++)
                        for (int z = Mathf.Min(randomNums[4], randomNums[5]); z <= Mathf.Max(randomNums[4], randomNums[5]); z++)
                            points[x, y, z] = fill;
            }

            for (int x = 0; x < maxX; x++)
                for (int y = 0; y < maxY; y++)
                    for (int z = 0; z < maxZ; z++)
                    {
                        if ((x == 1 || x == 2 || x == 15 || x == 16 || x == maxX - 2 || x == maxX - 3) && (z == 1 || z == 2 || z == 15 || z == 16 || z == maxZ - 2 || z == maxZ - 3) && y == maxY - 2)
                            for (int a = 0; a < players; a++)
                                if (Mathf.Abs(spawnPositions[a].x - x) < 2 && Mathf.Abs(spawnPositions[a].z - z) < 2) points[x, y, z] = true; //make start platforms
                        if (x == 0 || y == 0 || z == 0 || x == maxX - 1 || y == maxY - 1 || z == maxZ - 1) points[x, y, z] = false;
                    }
        }

        else { SetMapPoints(1); }
    }

    void UpdateChunks(List<int> chunksToUpdate)
    {
        int index = 0;
        for (int x = 0; x < xChunks; x++)
        {
            for (int y = 0; y < yChunks; y++)
            {
                for (int z = 0; z < zChunks; z++)
                {
                    bool update = false;
                    for(int a = 0; a < chunksToUpdate.Count; a++)
                        if (chunksToUpdate[a] == index)
                            update = true;

                    if (index >= chunks.Count || update || chunksToUpdate.Count == 0)
                    {
                        //add chunk or update chunk
                        //Vector3 dimensions = new Vector3((float)maxX / xChunks, (float)maxY / yChunks, (float)maxZ / zChunks);
                        Vector3 dimensions = new Vector3(16, 16, 16);
                        Vector3 origin = new Vector3(x * dimensions.x, y * dimensions.y, z * dimensions.z);
                        UpdateMesh(index, origin, dimensions);
                    }

                    index++;
                }
            }
        }
    }

    void UpdateMesh(int index, Vector3 origin, Vector3 dimensions)
    {
        GameObject chunk;
        if(index >= chunks.Count)
        {
            chunk = Instantiate(chunkPrefab, Vector3.zero, Quaternion.identity);
            chunks.Add(chunk);
            chunkOrigin.Add(new Vector3((int)origin.x, (int)origin.y, (int)origin.z));
        }

        else
            chunk = chunks[index];

        Mesh mesh;
        if (chunk.GetComponent<MeshFilter>().sharedMesh != null)
            mesh = chunk.GetComponent<MeshFilter>().sharedMesh;
        else
            mesh = new Mesh();
        chunk.GetComponent<MeshFilter>().sharedMesh = mesh;

        Vector3[] corners = new Vector3[] { new Vector3(0, 0, 0), new Vector3(0, 0, 1), new Vector3(1, 0, 1), new Vector3(1, 0, 0),
                                            new Vector3(0, 1, 0), new Vector3(0, 1, 1), new Vector3(1, 1, 1), new Vector3(1, 1, 0)};

        Vector3[] edges = new Vector3[] { new Vector3(0, 0, .5f), new Vector3(.5f, 0, 1), new Vector3(1, 0, .5f), new Vector3(.5f, 0, 0),
                                          new Vector3(0, 1, .5f), new Vector3(.5f, 1, 1), new Vector3(1, 1, .5f), new Vector3(.5f, 1, 0),
                                          new Vector3(0, .5f, 0), new Vector3(0, .5f, 1), new Vector3(1, .5f, 1), new Vector3(1, .5f, 0)};

        Vector2[] uvMap = new Vector2[] { new Vector2(0, 0), new Vector2(1, 0), new Vector2(0, 1) };
        Vector2[] uvMap2 = new Vector2[] { new Vector2(1, 0), new Vector2(0, 1), new Vector2(1, 1) };

        int counter = 0;
        int emptyCounter = 0;
        bool emptyMesh = true;

        for(int a = 0; a < verticies.Length; a++)
        {
            verticies[a] = Vector3.zero;
            uvs[a] = Vector2.zero;
        }

        //march through cubes
        for (int x = (int)origin.x; x < Mathf.Min((int)origin.x + (int)dimensions.x, maxX - 1); x++)
        {
            for (int y = (int)origin.y; y < Mathf.Min((int)origin.y + (int)dimensions.y, maxY - 1); y++)
            {
                for (int z = (int)origin.z; z < Mathf.Min((int)origin.z + (int)dimensions.z, maxZ - 1); z++)
                {
                    //find triangluation index
                    int cubeOrientation = 0;
                    for (int a = 0; a < 8; a++)
                        if (points[(int)corners[a].x + x, (int)corners[a].y + y, (int)corners[a].z + z] == false)
                            cubeOrientation += 1 << a;
                        else
                            emptyMesh = false;

                    //find triangluation
                    bool firstUV = Random.value > .5f;
                    for (int a = 0; a < 16; a++)
                    {
                        int triNum = triangulation[cubeOrientation, a];
                        if(triNum == -1)
                            emptyCounter++;
                        else
                        {
                            Vector3 cubeOrigin = new Vector3(x, y, z);
                            verticies[counter - emptyCounter] = (edges[triNum] + cubeOrigin);
                            triangles[counter - emptyCounter] = counter - emptyCounter;

                            float step = (float)1 / 16;
                            if (firstUV) uvs[counter - emptyCounter] = new Vector2(0.01f, step * Mathf.Min(y, 15) + .005f) + (step - .01f) * uvMap[a % 3];
                            else uvs[counter - emptyCounter] = new Vector2(0.01f, step * Mathf.Min(y, 15) + .005f) + (step - .01f) * uvMap2[a % 3];
                        }

                        counter++;
                    }
                }
            }
        }

        //create mesh
        mesh.Clear();
        mesh.vertices = verticies;
        mesh.triangles = triangles;
        mesh.uv = uvs;
        mesh.RecalculateNormals();
        if (chunk.GetComponent<MeshCollider>() != null)
            Destroy(chunk.GetComponent<MeshCollider>());
        if(!emptyMesh)
            chunk.AddComponent<MeshCollider>();
    }

    [ClientRpc]
    public void RpcDamageMap(Vector3 hit, float radius)
    {
        List<int> chunksToUpdate = new List<int>();

        for(int x = Mathf.Max(0, Mathf.FloorToInt(hit.x - radius)); x < Mathf.Min(maxX, Mathf.CeilToInt(hit.x + radius)); x++)
        {
            for(int y = Mathf.Max(0, Mathf.FloorToInt(hit.y - radius)); y < Mathf.Min(maxY, Mathf.CeilToInt(hit.y + radius)); y++)
            {
                for(int z = Mathf.Max(0, Mathf.FloorToInt(hit.z - radius)); z < Mathf.Min(maxZ, Mathf.CeilToInt(hit.z + radius)); z++)
                {
                    if(Vector3.Distance(new Vector3(x,y,z), hit) < radius)
                    {
                        //remove terrain from point
                        points[x,y,z] = false;

                        //if the point is in a chunk add the chunk to the update list
                        for(int a = 0; a < chunks.Count; a++)
                        {
                            if(x >= chunkOrigin[a].x && x <= chunkOrigin[a].x + 16 && y >= chunkOrigin[a].y && y <= chunkOrigin[a].y + 16 && z >= chunkOrigin[a].z && z <= chunkOrigin[a].z + 16)
                            {
                                bool inList = false;
                                for(int b = 0; b < chunksToUpdate.Count; b++)
                                    if(chunksToUpdate[b] == a)
                                        inList = true;

                                if (!inList)
                                    chunksToUpdate.Add(a);
                            }
                        }
                    }
                }
            }
        }

        UpdateChunks(chunksToUpdate);
    }

    void OnDestroy() { DestroyChunks(); }

    void DestroyChunks()
    {
        for (int a = chunks.Count - 1; a >= 0; a--)
        {
            if(chunks[a] != null)
            {
                Destroy(chunks[a].GetComponent<MeshFilter>().sharedMesh);
                Destroy(chunks[a]);
                chunks.RemoveAt(a);
                chunkOrigin.RemoveAt(a);
            }
        }
    }

    /*void ReduceVerticies()
    {
        for (int a = verticies.Count - 1; a >= 1; a--)
        {
            for (int b = a - 1; b >= 0; b--)
            {
                if (verticies[a] == verticies[b])
                {
                    verticies.RemoveAt(a);
                    triangles[a] = b;
                    for (int c = 0; c < triangles.Count; c++)
                    {
                        if (triangles[c] > a)
                            triangles[c]--;
                        else if (triangles[c] == a)
                            triangles[c] = b;
                    }
                    break;
                }
            }
        }
    }*/

    int[,] triangulation = new int[,] {
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 8, 3, 9, 8, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 2, 10, 0, 2, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 8, 3, 2, 10, 8, 10, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 11, 2, 8, 11, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 11, 2, 1, 9, 11, 9, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 10, 1, 11, 10, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 10, 1, 0, 8, 10, 8, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 9, 0, 3, 11, 9, 11, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 3, 0, 7, 3, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 1, 9, 4, 7, 1, 7, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 4, 7, 3, 0, 4, 1, 2, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 2, 10, 9, 0, 2, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 10, 9, 2, 9, 7, 2, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
    { 8, 4, 7, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 4, 7, 11, 2, 4, 2, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 1, 8, 4, 7, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 7, 11, 9, 4, 11, 9, 11, 2, 9, 2, 1, -1, -1, -1, -1 },
    { 3, 10, 1, 3, 11, 10, 7, 8, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 11, 10, 1, 4, 11, 1, 0, 4, 7, 11, 4, -1, -1, -1, -1 },
    { 4, 7, 8, 9, 0, 11, 9, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
    { 4, 7, 11, 4, 11, 9, 9, 11, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 5, 4, 1, 5, 0, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 5, 4, 8, 3, 5, 3, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 1, 2, 10, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 2, 10, 5, 4, 2, 4, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 10, 5, 3, 2, 5, 3, 5, 4, 3, 4, 8, -1, -1, -1, -1 },
    { 9, 5, 4, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 11, 2, 0, 8, 11, 4, 9, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 5, 4, 0, 1, 5, 2, 3, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 1, 5, 2, 5, 8, 2, 8, 11, 4, 8, 5, -1, -1, -1, -1 },
    { 10, 3, 11, 10, 1, 3, 9, 5, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 5, 0, 8, 1, 8, 10, 1, 8, 11, 10, -1, -1, -1, -1 },
    { 5, 4, 0, 5, 0, 11, 5, 11, 10, 11, 0, 3, -1, -1, -1, -1 },
    { 5, 4, 8, 5, 8, 10, 10, 8, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 7, 8, 5, 7, 9, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 3, 0, 9, 5, 3, 5, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 7, 8, 0, 1, 7, 1, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 7, 8, 9, 5, 7, 10, 1, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 1, 2, 9, 5, 0, 5, 3, 0, 5, 7, 3, -1, -1, -1, -1 },
    { 8, 0, 2, 8, 2, 5, 8, 5, 7, 10, 5, 2, -1, -1, -1, -1 },
    { 2, 10, 5, 2, 5, 3, 3, 5, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 9, 5, 7, 8, 9, 3, 11, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 7, 9, 7, 2, 9, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
    { 2, 3, 11, 0, 1, 8, 1, 7, 8, 1, 5, 7, -1, -1, -1, -1 },
    { 11, 2, 1, 11, 1, 7, 7, 1, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 8, 8, 5, 7, 10, 1, 3, 10, 3, 11, -1, -1, -1, -1 },
    { 5, 7, 0, 5, 0, 9, 7, 11, 0, 1, 0, 10, 11, 10, 0, -1 },
    { 11, 10, 0, 11, 0, 3, 10, 5, 0, 8, 0, 7, 5, 7, 0, -1 },
    { 11, 10, 5, 7, 11, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 1, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 8, 3, 1, 9, 8, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 5, 2, 6, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 5, 1, 2, 6, 3, 0, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 6, 5, 9, 0, 6, 0, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 9, 8, 5, 8, 2, 5, 2, 6, 3, 2, 8, -1, -1, -1, -1 },
    { 2, 3, 11, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 0, 8, 11, 2, 0, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 1, 9, 2, 9, 11, 2, 9, 8, 11, -1, -1, -1, -1 },
    { 6, 3, 11, 6, 5, 3, 5, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 11, 0, 11, 5, 0, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
    { 3, 11, 6, 0, 3, 6, 0, 6, 5, 0, 5, 9, -1, -1, -1, -1 },
    { 6, 5, 9, 6, 9, 11, 11, 9, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 3, 0, 4, 7, 3, 6, 5, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 5, 10, 6, 8, 4, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 6, 5, 1, 9, 7, 1, 7, 3, 7, 9, 4, -1, -1, -1, -1 },
    { 6, 1, 2, 6, 5, 1, 4, 7, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 5, 5, 2, 6, 3, 0, 4, 3, 4, 7, -1, -1, -1, -1 },
    { 8, 4, 7, 9, 0, 5, 0, 6, 5, 0, 2, 6, -1, -1, -1, -1 },
    { 7, 3, 9, 7, 9, 4, 3, 2, 9, 5, 9, 6, 2, 6, 9, -1 },
    { 3, 11, 2, 7, 8, 4, 10, 6, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 10, 6, 4, 7, 2, 4, 2, 0, 2, 7, 11, -1, -1, -1, -1 },
    { 0, 1, 9, 4, 7, 8, 2, 3, 11, 5, 10, 6, -1, -1, -1, -1 },
    { 9, 2, 1, 9, 11, 2, 9, 4, 11, 7, 11, 4, 5, 10, 6, -1 },
    { 8, 4, 7, 3, 11, 5, 3, 5, 1, 5, 11, 6, -1, -1, -1, -1 },
    { 5, 1, 11, 5, 11, 6, 1, 0, 11, 7, 11, 4, 0, 4, 11, -1 },
    { 0, 5, 9, 0, 6, 5, 0, 3, 6, 11, 6, 3, 8, 4, 7, -1 },
    { 6, 5, 9, 6, 9, 11, 4, 7, 9, 7, 11, 9, -1, -1, -1, -1 },
    { 10, 4, 9, 6, 4, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 10, 6, 4, 9, 10, 0, 8, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 0, 1, 10, 6, 0, 6, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 3, 1, 8, 1, 6, 8, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
    { 1, 4, 9, 1, 2, 4, 2, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 1, 2, 9, 2, 4, 9, 2, 6, 4, -1, -1, -1, -1 },
    { 0, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 3, 2, 8, 2, 4, 4, 2, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 4, 9, 10, 6, 4, 11, 2, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 2, 2, 8, 11, 4, 9, 10, 4, 10, 6, -1, -1, -1, -1 },
    { 3, 11, 2, 0, 1, 6, 0, 6, 4, 6, 1, 10, -1, -1, -1, -1 },
    { 6, 4, 1, 6, 1, 10, 4, 8, 1, 2, 1, 11, 8, 11, 1, -1 },
    { 9, 6, 4, 9, 3, 6, 9, 1, 3, 11, 6, 3, -1, -1, -1, -1 },
    { 8, 11, 1, 8, 1, 0, 11, 6, 1, 9, 1, 4, 6, 4, 1, -1 },
    { 3, 11, 6, 3, 6, 0, 0, 6, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 4, 8, 11, 6, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 10, 6, 7, 8, 10, 8, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 7, 3, 0, 10, 7, 0, 9, 10, 6, 7, 10, -1, -1, -1, -1 },
    { 10, 6, 7, 1, 10, 7, 1, 7, 8, 1, 8, 0, -1, -1, -1, -1 },
    { 10, 6, 7, 10, 7, 1, 1, 7, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 6, 1, 6, 8, 1, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
    { 2, 6, 9, 2, 9, 1, 6, 7, 9, 0, 9, 3, 7, 3, 9, -1 },
    { 7, 8, 0, 7, 0, 6, 6, 0, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 3, 2, 6, 7, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 11, 10, 6, 8, 10, 8, 9, 8, 6, 7, -1, -1, -1, -1 },
    { 2, 0, 7, 2, 7, 11, 0, 9, 7, 6, 7, 10, 9, 10, 7, -1 },
    { 1, 8, 0, 1, 7, 8, 1, 10, 7, 6, 7, 10, 2, 3, 11, -1 },
    { 11, 2, 1, 11, 1, 7, 10, 6, 1, 6, 7, 1, -1, -1, -1, -1 },
    { 8, 9, 6, 8, 6, 7, 9, 1, 6, 11, 6, 3, 1, 3, 6, -1 },
    { 0, 9, 1, 11, 6, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 8, 0, 7, 0, 6, 3, 11, 0, 11, 6, 0, -1, -1, -1, -1 },
    { 7, 11, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 8, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 9, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 1, 9, 8, 3, 1, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 1, 2, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 3, 0, 8, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 9, 0, 2, 10, 9, 6, 11, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 11, 7, 2, 10, 3, 10, 8, 3, 10, 9, 8, -1, -1, -1, -1 },
    { 7, 2, 3, 6, 2, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 7, 0, 8, 7, 6, 0, 6, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 7, 6, 2, 3, 7, 0, 1, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 6, 2, 1, 8, 6, 1, 9, 8, 8, 7, 6, -1, -1, -1, -1 },
    { 10, 7, 6, 10, 1, 7, 1, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 7, 6, 1, 7, 10, 1, 8, 7, 1, 0, 8, -1, -1, -1, -1 },
    { 0, 3, 7, 0, 7, 10, 0, 10, 9, 6, 10, 7, -1, -1, -1, -1 },
    { 7, 6, 10, 7, 10, 8, 8, 10, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 8, 4, 11, 8, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 6, 11, 3, 0, 6, 0, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 6, 11, 8, 4, 6, 9, 0, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 4, 6, 9, 6, 3, 9, 3, 1, 11, 3, 6, -1, -1, -1, -1 },
    { 6, 8, 4, 6, 11, 8, 2, 10, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 3, 0, 11, 0, 6, 11, 0, 4, 6, -1, -1, -1, -1 },
    { 4, 11, 8, 4, 6, 11, 0, 2, 9, 2, 10, 9, -1, -1, -1, -1 },
    { 10, 9, 3, 10, 3, 2, 9, 4, 3, 11, 3, 6, 4, 6, 3, -1 },
    { 8, 2, 3, 8, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 4, 2, 4, 6, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 9, 0, 2, 3, 4, 2, 4, 6, 4, 3, 8, -1, -1, -1, -1 },
    { 1, 9, 4, 1, 4, 2, 2, 4, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 1, 3, 8, 6, 1, 8, 4, 6, 6, 10, 1, -1, -1, -1, -1 },
    { 10, 1, 0, 10, 0, 6, 6, 0, 4, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 6, 3, 4, 3, 8, 6, 10, 3, 0, 3, 9, 10, 9, 3, -1 },
    { 10, 9, 4, 6, 10, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 5, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 4, 9, 5, 11, 7, 6, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 0, 1, 5, 4, 0, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 7, 6, 8, 3, 4, 3, 5, 4, 3, 1, 5, -1, -1, -1, -1 },
    { 9, 5, 4, 10, 1, 2, 7, 6, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 6, 11, 7, 1, 2, 10, 0, 8, 3, 4, 9, 5, -1, -1, -1, -1 },
    { 7, 6, 11, 5, 4, 10, 4, 2, 10, 4, 0, 2, -1, -1, -1, -1 },
    { 3, 4, 8, 3, 5, 4, 3, 2, 5, 10, 5, 2, 11, 7, 6, -1 },
    { 7, 2, 3, 7, 6, 2, 5, 4, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 5, 4, 0, 8, 6, 0, 6, 2, 6, 8, 7, -1, -1, -1, -1 },
    { 3, 6, 2, 3, 7, 6, 1, 5, 0, 5, 4, 0, -1, -1, -1, -1 },
    { 6, 2, 8, 6, 8, 7, 2, 1, 8, 4, 8, 5, 1, 5, 8, -1 },
    { 9, 5, 4, 10, 1, 6, 1, 7, 6, 1, 3, 7, -1, -1, -1, -1 },
    { 1, 6, 10, 1, 7, 6, 1, 0, 7, 8, 7, 0, 9, 5, 4, -1 },
    { 4, 0, 10, 4, 10, 5, 0, 3, 10, 6, 10, 7, 3, 7, 10, -1 },
    { 7, 6, 10, 7, 10, 8, 5, 4, 10, 4, 8, 10, -1, -1, -1, -1 },
    { 6, 9, 5, 6, 11, 9, 11, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 6, 11, 0, 6, 3, 0, 5, 6, 0, 9, 5, -1, -1, -1, -1 },
    { 0, 11, 8, 0, 5, 11, 0, 1, 5, 5, 6, 11, -1, -1, -1, -1 },
    { 6, 11, 3, 6, 3, 5, 5, 3, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 10, 9, 5, 11, 9, 11, 8, 11, 5, 6, -1, -1, -1, -1 },
    { 0, 11, 3, 0, 6, 11, 0, 9, 6, 5, 6, 9, 1, 2, 10, -1 },
    { 11, 8, 5, 11, 5, 6, 8, 0, 5, 10, 5, 2, 0, 2, 5, -1 },
    { 6, 11, 3, 6, 3, 5, 2, 10, 3, 10, 5, 3, -1, -1, -1, -1 },
    { 5, 8, 9, 5, 2, 8, 5, 6, 2, 3, 8, 2, -1, -1, -1, -1 },
    { 9, 5, 6, 9, 6, 0, 0, 6, 2, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 5, 8, 1, 8, 0, 5, 6, 8, 3, 8, 2, 6, 2, 8, -1 },
    { 1, 5, 6, 2, 1, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 3, 6, 1, 6, 10, 3, 8, 6, 5, 6, 9, 8, 9, 6, -1 },
    { 10, 1, 0, 10, 0, 6, 9, 5, 0, 5, 6, 0, -1, -1, -1, -1 },
    { 0, 3, 8, 5, 6, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 5, 6, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 5, 10, 7, 5, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 5, 10, 11, 7, 5, 8, 3, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 11, 7, 5, 10, 11, 1, 9, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 10, 7, 5, 10, 11, 7, 9, 8, 1, 8, 3, 1, -1, -1, -1, -1 },
    { 11, 1, 2, 11, 7, 1, 7, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 1, 2, 7, 1, 7, 5, 7, 2, 11, -1, -1, -1, -1 },
    { 9, 7, 5, 9, 2, 7, 9, 0, 2, 2, 11, 7, -1, -1, -1, -1 },
    { 7, 5, 2, 7, 2, 11, 5, 9, 2, 3, 2, 8, 9, 8, 2, -1 },
    { 2, 5, 10, 2, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 2, 0, 8, 5, 2, 8, 7, 5, 10, 2, 5, -1, -1, -1, -1 },
    { 9, 0, 1, 5, 10, 3, 5, 3, 7, 3, 10, 2, -1, -1, -1, -1 },
    { 9, 8, 2, 9, 2, 1, 8, 7, 2, 10, 2, 5, 7, 5, 2, -1 },
    { 1, 3, 5, 3, 7, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 7, 0, 7, 1, 1, 7, 5, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 0, 3, 9, 3, 5, 5, 3, 7, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 8, 7, 5, 9, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 8, 4, 5, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 5, 0, 4, 5, 11, 0, 5, 10, 11, 11, 3, 0, -1, -1, -1, -1 },
    { 0, 1, 9, 8, 4, 10, 8, 10, 11, 10, 4, 5, -1, -1, -1, -1 },
    { 10, 11, 4, 10, 4, 5, 11, 3, 4, 9, 4, 1, 3, 1, 4, -1 },
    { 2, 5, 1, 2, 8, 5, 2, 11, 8, 4, 5, 8, -1, -1, -1, -1 },
    { 0, 4, 11, 0, 11, 3, 4, 5, 11, 2, 11, 1, 5, 1, 11, -1 },
    { 0, 2, 5, 0, 5, 9, 2, 11, 5, 4, 5, 8, 11, 8, 5, -1 },
    { 9, 4, 5, 2, 11, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 5, 10, 3, 5, 2, 3, 4, 5, 3, 8, 4, -1, -1, -1, -1 },
    { 5, 10, 2, 5, 2, 4, 4, 2, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 10, 2, 3, 5, 10, 3, 8, 5, 4, 5, 8, 0, 1, 9, -1 },
    { 5, 10, 2, 5, 2, 4, 1, 9, 2, 9, 4, 2, -1, -1, -1, -1 },
    { 8, 4, 5, 8, 5, 3, 3, 5, 1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 4, 5, 1, 0, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 8, 4, 5, 8, 5, 3, 9, 0, 5, 0, 3, 5, -1, -1, -1, -1 },
    { 9, 4, 5, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 11, 7, 4, 9, 11, 9, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 8, 3, 4, 9, 7, 9, 11, 7, 9, 10, 11, -1, -1, -1, -1 },
    { 1, 10, 11, 1, 11, 4, 1, 4, 0, 7, 4, 11, -1, -1, -1, -1 },
    { 3, 1, 4, 3, 4, 8, 1, 10, 4, 7, 4, 11, 10, 11, 4, -1 },
    { 4, 11, 7, 9, 11, 4, 9, 2, 11, 9, 1, 2, -1, -1, -1, -1 },
    { 9, 7, 4, 9, 11, 7, 9, 1, 11, 2, 11, 1, 0, 8, 3, -1 },
    { 11, 7, 4, 11, 4, 2, 2, 4, 0, -1, -1, -1, -1, -1, -1, -1 },
    { 11, 7, 4, 11, 4, 2, 8, 3, 4, 3, 2, 4, -1, -1, -1, -1 },
    { 2, 9, 10, 2, 7, 9, 2, 3, 7, 7, 4, 9, -1, -1, -1, -1 },
    { 9, 10, 7, 9, 7, 4, 10, 2, 7, 8, 7, 0, 2, 0, 7, -1 },
    { 3, 7, 10, 3, 10, 2, 7, 4, 10, 1, 10, 0, 4, 0, 10, -1 },
    { 1, 10, 2, 8, 7, 4, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 1, 4, 1, 7, 7, 1, 3, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 9, 1, 4, 1, 7, 0, 8, 1, 8, 7, 1, -1, -1, -1, -1 },
    { 4, 0, 3, 7, 4, 3, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 4, 8, 7, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 10, 8, 10, 11, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 9, 3, 9, 11, 11, 9, 10, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 1, 10, 0, 10, 8, 8, 10, 11, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 1, 10, 11, 3, 10, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 2, 11, 1, 11, 9, 9, 11, 8, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 0, 9, 3, 9, 11, 1, 2, 9, 2, 11, 9, -1, -1, -1, -1 },
    { 0, 2, 11, 8, 0, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 3, 2, 11, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 8, 2, 8, 10, 10, 8, 9, -1, -1, -1, -1, -1, -1, -1 },
    { 9, 10, 2, 0, 9, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 2, 3, 8, 2, 8, 10, 0, 1, 8, 1, 10, 8, -1, -1, -1, -1 },
    { 1, 10, 2, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 1, 3, 8, 9, 1, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 9, 1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    { 0, 3, 8, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 },
    {-1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1, -1 }
};
}
