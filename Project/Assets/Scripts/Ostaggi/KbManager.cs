using UnityEngine;
using Neo4j.Driver;
using System.Threading.Tasks;
using System;
using System.Collections.Generic;


public class KbManager : MonoBehaviour
{
    private IDriver _driver;

    public static KbManager Instance;

    private int cellSize = 5;
    private string db_name = "dispersi";

    public bool IsReady { get; private set; } = false;
    private Dictionary<Vector2, Color> _cellColors = new Dictionary<Vector2, Color>();
    private readonly object _cellColorsLock = new object();

    private DateTime _lastUpdateTime;
    void Awake()
    {
        if (Instance == null)
            Instance = this;
        else
            Destroy(gameObject);

        try
        {
            Debug.Log("Attempting to connect to Neo4j...");
            _driver = GraphDatabase.Driver("bolt://localhost:7687",
                      AuthTokens.Basic("neo4j", "password"));
            Debug.Log("Connected to Neo4j successfully!");
        }
        catch (Exception ex)
        {
            Debug.LogError("Failed to connect to Neo4j: " + ex.Message);
        }
    }

    async void Start()
    {
        await InitializeGrid(150);
        await InitializeHostages();
        await ResetKnowledgeBase();
        await UpdateCellColorsCache();
        // set isReady to true
        IsReady = true;
    }

    private float snapPosition(float coord)
    {
        float offset = cellSize / 2f; // 2.5 se cellSize è 5
        return Mathf.Round((coord - offset) / cellSize) * cellSize + offset;
    }

    public async Task InitializeGrid(int areaDiameter)
    {
        Debug.Log("Initializing grid in Neo4j...");
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        try
        {
            // remove all obstacles and enemies from graph
            await session.RunAsync(@"
                MATCH (o:Obstacle)
                DETACH DELETE o
            ");

            // Set blocks relationship between obstacles and cells
            GameObject[] blockedObjects = GameObject.FindGameObjectsWithTag("Obstacle");
            foreach (var obj in blockedObjects)
            {
                Vector3 position = obj.transform.position;
                //SnapObstacleToGrid(obj);

                // Controlla se l'ostacolo è all'interno dell'area di gioco.
                if (position.x < -areaDiameter / 2 || position.x >= areaDiameter / 2 ||
                    position.z < -areaDiameter / 2 || position.z >= areaDiameter / 2)
                {
                    //Debug.Log($"Skipping obstacle {obj.name} fuori dall'area: {position}");
                    continue;  // Salta questo ostacolo se è fuori dai limiti
                }

                //Debug.Log("Found obstacle: " + obj.name + " / " + blockedObjects.Length);

                // Supponiamo che tu abbia un metodo per ottenere le dimensioni in celle
                int width = GetWidthInCells(obj);
                int height = GetHeightInCells(obj);

                // "Snap" della posizione alle coordinate della griglia
                float snappedX = snapPosition(position.x);
                float snappedZ = snapPosition(position.z);

                float effectiveSnappedX = (width % 2 == 0) ? snappedX - cellSize / 2f : snappedX;
                float effectiveSnappedZ = (height % 2 == 0) ? snappedZ - cellSize / 2f : snappedZ;

                await session.RunAsync(@"
                    MERGE (o:Obstacle {id: $id})
                    SET o.name = $name, o.width = $width, o.height = $height
                ", new
                {
                    id = obj.GetInstanceID(),
                    name = obj.name,
                    x = effectiveSnappedX,
                    z = effectiveSnappedZ,
                    width,
                    height
                });

                Debug.Log("obstacle name " + obj.name + " width normal position " + obj.transform.position.x + " snapped position " + snappedX + " height normal position " + obj.transform.position.z + " snapped position " + snappedZ);

                // Poi calcola l'angolo in alto a sinistra (startX, startZ)
                float startX = effectiveSnappedX - ((width - 1) / 2f * cellSize);
                float startZ = effectiveSnappedZ - ((height - 1) / 2f * cellSize);

                // Connettiamo tutte le celle occupate
                for (int dx = 0; dx < width; dx++)
                {
                    for (int dz = 0; dz < height; dz++)
                    {
                        float cellX = startX + dx * cellSize;
                        float cellZ = startZ + dz * cellSize;

                        // Se la griglia è definita da 0 a areaDiameter (es. 150)
                        if (cellX < -areaDiameter / 2 || cellX > areaDiameter / 2 ||
                            cellZ < -areaDiameter / 2 || cellZ > areaDiameter / 2)
                        {
                            // Salta le celle fuori dall'area di gioco
                            continue;
                        }

                        await session.RunAsync(@"
                            MATCH (o:Obstacle {id: $id})
                            MATCH (c:Cell {x: $cellX, z: $cellZ})
                            MERGE (o)-[:BLOCKS]->(c)
                        ", new { id = obj.GetInstanceID(), cellX, cellZ });
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);

        }

        Debug.Log("Obstacles initialized in Neo4j.");
    }

    public async Task InitializeHostages()
    {
        Debug.Log("Initializing grid in Neo4j...");
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        try
        {
            // remove all obstacles and enemies from graph
            await session.RunAsync(@"
                MATCH (h:Hostage)
                DETACH DELETE h
            ");

            // Set blocks relationship between obstacles and cells
            GameObject[] blockedObjects = GameObject.FindGameObjectsWithTag("Hostage");
            foreach (var obj in blockedObjects)
            {
                Vector3 position = obj.transform.position;
                //Debug.Log("Found hostage: " + obj.name + " / " + blockedObjects.Length);

                // "Snap" della posizione alle coordinate della griglia
                float snappedX = snapPosition(position.x);
                float snappedZ = snapPosition(position.z);

                int width = GetWidthInCells(obj);
                int height = GetHeightInCells(obj);

                int priority = obj.GetComponent<Hostage>()?.Priority ?? 1;

                await session.RunAsync(@"
                    MERGE (h:Hostage {id: $id})
                    SET h.name = $name,
                        h.x = $x,
                        h.z = $z,
                        h.width = $width,
                        h.height = $height,
                        h.priority = $priority
                    WITH h
                    MATCH (c:Cell {x: $x, z: $z})
                    OPTIONAL MATCH (c)<-[r:BLOCKS]-(:Obstacle)
                    WITH h, c, COUNT(r) AS blockCount
                    CALL apoc.util.validate(blockCount > 0, 'Error: The cell is blocked by an obstacle', [blockCount])
                    MERGE (h)-[:RESTRICTED_ON]->(c)
                    RETURN h;
                ", new
                {
                    id = obj.GetInstanceID(),
                    name = obj.name,
                    x = snappedX,
                    z = snappedZ,
                    width,
                    height,
                    priority
                });
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }

        Debug.Log("Hostages initialized in Neo4j.");
    }

    public int GetWidthInCells(GameObject obj)
    {
        if (!obj.TryGetComponent<Collider>(out var collider))
        {
            Debug.LogWarning($"GameObject {obj.name} does not have a Collider.");
            return 1; // Se non c'è un collider, assumiamo che occupi 1 cella
        }

        float width = collider.bounds.size.x;
        int cells = Mathf.CeilToInt(width / cellSize);

        //Debug.Log($"Obstacle {obj.name}: width = {width} -> occupies {cells} cells.");
        return cells;
    }

    public int GetHeightInCells(GameObject obj)
    {
        if (!obj.TryGetComponent<Collider>(out var collider))
        {
            Debug.LogWarning($"GameObject {obj.name} does not have a Collider.");
            return 1; // Se non c'è un collider, assumiamo che occupi 1 cella
        }

        float height = collider.bounds.size.z;
        int cells = Mathf.CeilToInt(height / cellSize);

        //Debug.Log($"Obstacle {obj.name}: height = {height} -> occupies {cells} cells.");
        return cells;
    }

    public async Task ResetKnowledgeBase()
    {
        Debug.Log("Resetting knowledge base...");
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        await session.RunAsync(@"
            // Rimuove solo gli agenti senza toccare le celle
            MATCH (a)
            WHERE a:DroneAgent OR a:RobotAgent OR a:EnemyAgent OR a:Communication
            DETACH DELETE a;
        ");

    }

    public async Task UpdateDronePosition(string droneId, Vector3 pos)
    {
        float snappedX = snapPosition(pos.x);
        float snappedZ = snapPosition(pos.z);
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = $@"
            MATCH (a:DroneAgent {{id: $droneId}})
            SET a.x = $x, a.z = $z
            WITH a
            MATCH (c:Cell {{x: $x, z: $z}})
            MERGE (a)-[:VISITED]->(c)";
        await session.RunAsync(query, new { droneId, x = snappedX, z = snappedZ });
    }

    public async Task ConnectToRobot(string droneId, string robotId)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = @"
            // Aggiorna il nodo DroneAgent
            MERGE (d:DroneAgent {id: $droneId})

            // Aggiorna (o crea) il nodo Communication specifico
            MERGE (comm:Communication {id: $commId})

            // Collega il drone alla comunicazione come mittente
            MERGE (d)-[r:SEND]->(comm)
            WITH d, comm

            // Aggiunge gli elementi della comunicazione
            MERGE (r: RobotAgent {id: $robotId})
            MERGE (comm)<-[n:RECEIVE]-(r)
            RETURN comm
        ";

        await session.RunAsync(query, new
        {
            droneId,
            commId = "Comm_0",
            robotId,

        });

    }

    public async Task ReportEnemyPosition(string droneId, Vector3 enemyPos)
    {
        float snappedX = snapPosition(enemyPos.x);
        float snappedZ = snapPosition(enemyPos.z);

        try
        {
            using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
            string query = @"
            // Aggiorna il nodo DroneAgent
            MERGE (d:DroneAgent {id: $droneId})

            // Aggiorna (o crea) il nodo Communication specifico
            MERGE (comm:Communication {id: $commId})
            SET comm.lastReport = timestamp()

            // Collega il drone alla comunicazione come mittente
            MERGE (d)-[r:SEND]->(comm)
            WITH d, comm

            // Aggiunge gli elementi della comunicazione
            MATCH (e: EnemyAgent)
            MATCH (r: RobotAgent)
            MERGE (comm)<-[n:RECEIVE]-(r)
            MERGE (comm)-[:HAS_SUBJECT]->(e)
            WITH e

            // Rimuove la vecchia relazione LOCATED_ON (se_esistente)
            OPTIONAL MATCH (e)-[oldRel: LOCATED_ON]->(oldCell :Cell)
            DELETE oldRel
            WITH e

            // Aggiorna la cella dove si trova l'enemy
            MATCH (c:Cell {x: $ex, z: $ez})
            MERGE (e)-[:LOCATED_ON]->(c)

            RETURN e";

            await session.RunAsync(query, new
            {
                droneId = droneId,
                commId = "Comm_0",
                ex = snappedX,
                ez = snappedZ
            });
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }
        Debug.Log($"Drone {droneId} reported enemy position in Neo4j: {enemyPos}");
    }

    public async Task<Vector3?> InferBestSearchZone()
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        var cursor = await session.RunAsync(@"
            MATCH (e:EnemyAgent)-[:LOCATED_ON]->(c:Cell)
            RETURN c.x AS x, c.z AS z
        ");

        if (!await cursor.FetchAsync()) return null;

        var record = cursor.Current;
        return new Vector3(record["x"].As<float>(), 0, record["z"].As<float>());
    }

    public async Task<Vector3?> GetLeastVisitedZone(string agentType, string agentId)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        var cursor = await session.RunAsync(@"
            MATCH (c:Cell)
            WHERE NOT EXISTS { MATCH (a:DroneAgent)-[:VISITED]->(c) }
            RETURN c.x AS x, c.z AS z
            ORDER BY rand() // Seleziona una cella a caso tra quelle non visitate
            LIMIT 1",
            new { agentId });

        if (!await cursor.FetchAsync()) return null;

        var record = cursor.Current;
        return new Vector3(record["x"].As<float>(), 0, record["z"].As<float>());
    }

    public async Task SetHostageKnowledge(string agentType, string agentId)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = $@"
            MERGE (e:{agentType})
            SET e.id = '{agentId}'
            WITH e
            MATCH (h:Hostage)-[:RESTRICTED_ON]->(:Cell)
            MERGE (e)-[:KNOWS]->(h)";

        try
        {
            await session.RunAsync(query, new { agentId });
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }

    }

    public async Task UpdateEnemyAgentPosition(string agentType, string agentId, Vector3 pos)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = $@"
            MERGE (a:{agentType} {{id: $agentId}})
            SET a.x = $x, a.z = $z
            WITH a
            MATCH (c:Cell {{x: $x, z: $z}})";

        try
        {
            await session.RunAsync(query, new { agentId, x = pos.x, z = pos.z });
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }

    }

    public async Task DetectedRobotAgent(Vector3 pos)
    {
        //Debug.Log(snapPosition(pos.x) + ", " + snapPosition(pos.z));
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        await session.RunAsync(@"
            MERGE (e:EnemyAgent)
            REMOVE e.lastRobotPosX, e.lastRobotPosZ
        ", new { posX = snapPosition(pos.x), posZ = snapPosition(pos.z) });
    }

    public async Task ResetDetectedRobotAgent()
    {
        //Debug.Log(snapPosition(pos.x) + ", " + snapPosition(pos.z));
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        await session.RunAsync(@"
            MERGE (e:EnemyAgent)
            SET e.lastRobotPosX = -9999,
                e.lastRobotPosZ = -9999
        ");
    }

    public async Task<List<Vector3>> GetAvailableHostagePositionsByPriority(string agentType)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = "";
        if (agentType == "EnemyAgent")
        {
            query = $@"
                MATCH (a: {agentType})-[:KNOWS]->(h: Hostage)
                RETURN h.x AS x, h.z AS z
                ORDER BY h.priority DESC
            ";

        }
        else if (agentType == "RobotAgent")
        {
            query = $@"
                MATCH (a: {agentType})-[:KNOWS]->(h: Hostage)-[:RESTRICTED_ON]->(c:Cell)
                RETURN c.x AS x, c.z AS z
                ORDER BY h.priority DESC
            ";

        }

        var hostagePositions = new List<Vector3>();

        try
        {
            var cursor = await session.RunAsync(query);


            while (await cursor.FetchAsync())
            {
                var record = cursor.Current;
                float x = record["x"].As<float>();
                float z = record["z"].As<float>();
                hostagePositions.Add(new Vector3(x, 0, z));
            }

        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }

        return hostagePositions;
    }

    public async Task<Vector3?> GetNearestHostage(Vector3 position)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        var cursor = await session.RunAsync(@"
            MATCH (h:Hostage)<-[:KNOWS]-(e:EnemyAgent)
            WITH h, e, point.distance(
                point({x: h.x, y: 0, z: h.z}), 
                point({x: $posX, y: 0, z: $posZ})
            ) AS d
            ORDER BY d ASC, h.priority DESC
            LIMIT 1
            RETURN h.x AS x, h.z AS z
        ", new { posX = position.x, posZ = position.z });

        if (!await cursor.FetchAsync()) return null;

        var record = cursor.Current;
        return new Vector3(record["x"].As<float>(), 0, record["z"].As<float>());
    }

    public async Task ForgetHostage(Vector3 pos)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        string query = @"
            MATCH (e:EnemyAgent)-[k:KNOWS]->(h:Hostage {x: $x, z: $z})
            DELETE k";
        await session.RunAsync(query, new { x = pos.x, z = pos.z });
    }

    public async Task<bool> IsHostagePresent(Vector3 position)
    {
        Debug.Log("POS_ " + position);
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        var cursor = await session.RunAsync(@"
            MATCH (h:Hostage)-[:RESTRICTED_ON]->(c:Cell {x: $x, z: $z})
            RETURN h IS NOT NULL AS isPresent
        ", new { x = position.x, z = position.z });

        if (await cursor.FetchAsync())
        {
            var record = cursor.Current;
            return record["isPresent"].As<bool>();
        }

        return false;
    }

    public async Task<List<Vector3>> FindShortestPath(string agentId, Vector3 start, Vector3 goal)
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
        // Debug.Log($"Finding shortest path in Neo4j for {goal}");
        List<Vector3> path = null;
        try
        {
            var checkResult = await session.RunAsync($"CALL gds.graph.exists('searchGraph{agentId}') YIELD exists RETURN exists");
            var checkRecords = await checkResult.ToListAsync();
            bool graphExists = checkRecords.Count > 0 && checkRecords[0]["exists"].As<bool>();

            if (graphExists)
            {
                await session.RunAsync($"CALL gds.graph.drop('searchGraph{agentId}') YIELD graphName RETURN graphName");
            }
            //Debug.Log("Dropped existing graph projection.");
            // Crea una nuova proiezione in memoria con una cypher projection che prenda in considerazione le informazioni aggiornate
            await session.RunAsync($@"
                CALL gds.graph.project.cypher(
                    'searchGraph{agentId}',
                    'MATCH (r: EnemyAgent)
                    WITH coalesce(r.lastRobotPosX, -9999) AS robotX, coalesce(r.lastRobotPosZ, -9999) AS robotZ
                    MATCH (n:Cell) 
                    WHERE n.x <> robotX AND n.z <> robotZ
                        AND NOT ( (n)<-[:LOCATED_ON|BLOCKS]-(:Enemy) )
                        AND NOT ( (n)<-[:LOCATED_ON|BLOCKS]-(:Obstacle) )
                    RETURN id(n) AS id, n.x AS x, n.z AS z',
                    'MATCH (r: EnemyAgent)
                    WITH coalesce(r.lastRobotPosX, -9999) AS robotX, coalesce(r.lastRobotPosZ, -9999) AS robotZ
                    MATCH (n1:Cell)-[r:ADJACENT_TO]->(n2:Cell)
                    WHERE n1.x <> robotX AND n1.z <> robotZ AND n2.x <> robotX AND n2.z <> robotZ
                        AND NOT ( (n1)<-[:LOCATED_ON|BLOCKS]-(:Enemy) OR (n1)<-[:LOCATED_ON|BLOCKS]-(:Obstacle) )
                        AND NOT ( (n2)<-[:LOCATED_ON|BLOCKS]-(:Enemy) OR (n2)<-[:LOCATED_ON|BLOCKS]-(:Obstacle) )
                    RETURN id(n1) AS source, id(n2) AS target, coalesce(r.cost, 1.0) AS cost'
                )
            ");
            //Debug.Log("Created new graph projection.");

            // Esegui la ricerca del percorso
            var result = await session.RunAsync(@"
                MATCH (start:Cell {x: $startX, z: $startZ}), (goal:Cell {x: $goalX, z: $goalZ})
                CALL gds.shortestPath.astar.stream('searchGraph" + agentId + @"', {
                    sourceNode: id(start),
                    targetNode: id(goal),
                    relationshipWeightProperty: 'cost',
                    latitudeProperty: 'x', 
                    longitudeProperty: 'z'
                })
                YIELD nodeIds
                RETURN nodeIds;
            ", new
            {
                startX = snapPosition(start.x),
                startZ = snapPosition(start.z),
                goalX = snapPosition(goal.x),
                goalZ = snapPosition(goal.z)
            });

            path = new List<Vector3>();

            // Raccogli tutti i record in una lista
            var records = await result.ToListAsync();

            foreach (var record in records)
            {
                var nodeIds = record["nodeIds"].As<List<long>>();
                //Debug.Log("Found shortest path, Records count: " + records.Count + " from " + snapPosition(start.x) + "," + snapPosition(start.z) + " to " + snapPosition(goal.x) + "," + snapPosition(goal.z));
                foreach (var nodeId in nodeIds)
                {
                    var nodeResult = await session.RunAsync(
                        "MATCH (c:Cell) WHERE id(c) = $nodeId RETURN c.x AS x, c.z AS z",
                        new { nodeId });
                    var nodeRecords = await nodeResult.ToListAsync();
                    foreach (var r in nodeRecords)
                    {
                        float x = r["x"].As<float>();
                        float z = r["z"].As<float>();
                        // Debug.Log($"Found cell: x = {x}, z = {z}");
                        path.Add(new Vector3(x, 0, z));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }

        // Debug.Log("Path found with " + path.Count + " steps.");
        return path;
    }

    public async Task<Vector3?> GetEnemyPosition()
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        var cursor = await session.RunAsync(@"
            MATCH (r: RobotAgent)-[:RECEIVE]->(comm: Communication)<-[:SEND]-(:DroneAgent)
            MATCH (comm)-[:HAS_SUBJECT]->(e: EnemyAgent)
            MATCH (e)-[:LOCATED_ON]-(c :Cell)
            RETURN c.x AS x, c.z AS z
            LIMIT 1");

        if (!await cursor.FetchAsync()) return null;

        var record = cursor.Current;
        return new Vector3(record["x"].As<float>(), 0, record["z"].As<float>());
    }

    public async Task<Vector3?> GetDronePosition()
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        var cursor = await session.RunAsync(@"
            MATCH (d:DroneAgent)-[:SEND]->(comm:Communication)-[:RECEIVE]-(:RobotAgent)
            RETURN d.x AS x, d.z AS z");

        if (!await cursor.FetchAsync()) return null;

        var record = cursor.Current;
        return new Vector3(record["x"].As<float>(), 0, record["z"].As<float>());
    }

    public async Task FreeHostage(Vector3 pos)
    {
        Debug.Log(pos);
        try
        {
            using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
            string query = @"
            MATCH (h:Hostage )-[r:RESTRICTED_ON]->(c:Cell {x: $x, z: $z})
            DELETE r";
            await session.RunAsync(query, new { x = pos.x, z = pos.z });
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }
    }

    public async Task GameOver(bool isWin)
    {
        try
        {
            using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
            string query = @"
                MERGE (o:Outcome {id: 'GameOutcome'})
                SET o.win = $isWin
            
                WITH o
                MATCH (r:RobotAgent), (e:EnemyAgent), (d:DroneAgent)
                
                MERGE (r)-[:FOLLOW]->(o)
                MERGE (e)-[:FOLLOW]->(o)
                MERGE (d)-[:FOLLOW]->(o)
                
                RETURN o";
            await session.RunAsync(query, new { isWin });
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
        }
    }

    public async Task<bool> isGameOver(string agentId)
    {
        try
        {
            using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));
            var result = await session.RunAsync(@"
                MATCH (a {id: $agentId})-[:FOLLOW]->(o:Outcome)
                RETURN o.win IS NOT NULL as isOver
            ", new { agentId });

            var records = await result.ToListAsync();
            if (records.Count > 0)
            {
                bool gameOver = records[0]["isOver"].As<bool>();
                return gameOver;
            }
            else
            {
                return false;
            }
        }
        catch (Exception ex)
        {
            Debug.LogError("An error occurred: " + ex.Message);
            return false;
        }
    }


    private async void OnDestroy()
    {
        if (_driver != null)
        {
            IsReady = false;
            await _driver.DisposeAsync();
            Debug.Log("Disconnected from Neo4j.");
        }
    }

    private async Task UpdateCellColorsCache()
    {
        using var session = _driver.AsyncSession(o => o.WithDatabase(db_name));

        var result = await session.RunAsync(@"
            MATCH (c:Cell)
            OPTIONAL MATCH (c)<-[:BLOCKS]-(o:Obstacle)
            OPTIONAL MATCH (c)<-[:LOCATED_ON]-(h:Hostage)
            RETURN c.x AS x, c.z AS z,
                CASE 
                    WHEN COUNT(o) > 0 THEN 'red'
                    WHEN COUNT(h) > 0 THEN 'green'
                    ELSE 'gray'
                END AS color
        ");

        var newCellColors = new Dictionary<Vector2, Color>();
        await result.ForEachAsync(record =>
        {
            var key = new Vector2(record["x"].As<float>(), record["z"].As<float>());
            newCellColors[key] = record["color"].As<string>() switch
            {
                "red" => Color.red,
                "green" => Color.green,
                _ => Color.gray
            };
        });

        lock (_cellColorsLock)
        {
            _cellColors = newCellColors;
        }

        _lastUpdateTime = DateTime.Now;
    }

    public void OnDrawGizmos()
    {
        if (!IsReady)
        {
            Gizmos.color = Color.red;

            // Numero di celle per lato
            int numCells = 30;
            // Calcola l'offset per centrare le celle (metà cella)
            float offset = cellSize * 0.5f; // per cellSize = 5, offset = 2.5
                                            // Calcola il punto di partenza: il bordo sinistro della griglia è a -75, 
                                            // ma il centro della prima cella deve essere a -75 + offset = -72.5
            float start = -75 + offset;

            for (int i = 0; i < numCells; i++)
            {
                for (int j = 0; j < numCells; j++)
                {
                    float x = start + i * cellSize;
                    float z = start + j * cellSize;
                    Gizmos.DrawWireCube(new Vector3(x, 0.5f, z), new Vector3(cellSize, 0.5f, cellSize));
                }
            }
        }

        else
        {
            if ((DateTime.Now - _lastUpdateTime).TotalSeconds > 1)
            {
                _ = UpdateCellColorsCache();
            }
            // Disegna le celle
            lock (_cellColorsLock)
            {
                // Disegna le celle
                foreach (var entry in _cellColors)
                {
                    Vector3 cellCenter = new Vector3(entry.Key.x, 0.2f, entry.Key.y);

                    Gizmos.color = entry.Value;
                    Gizmos.DrawCube(cellCenter, new Vector3(cellSize, 0.2f, cellSize));

                    Gizmos.color = Color.white;
                    Gizmos.DrawWireCube(cellCenter, new Vector3(cellSize, 0.2f, cellSize));
                }
            }
        }
    }

}
