using System.Collections;
using System.Collections.Generic;
using System;
using UnityEngine;

struct Coord {
	public int tileX;
	public int tileY;

	public Coord(int x, int y){
		tileX = x;
		tileY = y;
	}
}

class Room : IComparable<Room> {
	public List<Coord> tiles;
	public List<Coord> edgeTiles;
	public List<Room> connectedRooms;
	public int roomSize;

	public bool connectedToMain;
	public bool isMain;
	public Room() {
	}

	public Room(List<Coord> roomTiles, Voxel[,] map) {
		tiles = roomTiles;
		roomSize = tiles.Count;
		connectedRooms = new List<Room>();
		
		edgeTiles = new List<Coord>();
		foreach (Coord tile in tiles) {
			for (int x = tile.tileX-1; x <= tile.tileX+1; x++) {
				for (int y = tile.tileY-1; y <= tile.tileY+1; y++) {
					if (x == tile.tileX || y == tile.tileY) {
						if (map[x,y].state) {
							edgeTiles.Add(tile);
						}
					}
				}
	 		}
		}
	}

	public void SetAccessibleFromMainRoom() {
		if (!connectedToMain) {
			connectedToMain = true;
			foreach (Room connectedRoom in connectedRooms) {
				connectedRoom.SetAccessibleFromMainRoom();
			}
		}
	}

	public static void ConnectRooms(Room roomA, Room roomB) {
		if (roomA.connectedToMain) {
			roomB.SetAccessibleFromMainRoom ();
		} else if (roomB.connectedToMain) {
			roomA.SetAccessibleFromMainRoom();
		}
		roomA.connectedRooms.Add (roomB);
		roomB.connectedRooms.Add (roomA);
	}
	
	public bool IsConnected(Room otherRoom) {
		return connectedRooms.Contains(otherRoom);
	}
	
	public int CompareTo(Room otherRoom) {
		return otherRoom.roomSize.CompareTo (roomSize);
	}
}

public class GenerateProcedural : MonoBehaviour {

    public int width, height;
    public int seed;

    public float voxelSize;

    public GameObject voxelPrefab;

	public bool connectRooms;

    [Range(0,100)]
    public float randomFillPercent;

    private Material[,] voxelMaterials;

    private Voxel[,] voxels;

    private Mesh mesh;

	private List<Vector3> vertices;
	private List<int> triangles;
    private Voxel dummyX, dummyY, dummyT;

    System.Random rand;

    private void Start(){
        GenerateMap();
    }

	private void Update(){
		if(Input.GetMouseButtonDown(0)){
			RandomFillMap();
		}
	}
    public void GenerateMap(){
        voxels = new Voxel[width, height];
        voxelMaterials = new Material[width, height];

		seed = -1573646140;
        //seed =  (int)System.DateTime.Now.Ticks; 
        rand = new System.Random(seed.GetHashCode());
           
		dummyX = new Voxel();
		dummyY = new Voxel();
		dummyT = new Voxel();

        GetComponent<MeshFilter>().mesh = mesh = new Mesh();
		mesh.name = "VoxelGrid Mesh";
		vertices = new List<Vector3>();
		triangles = new List<int>();

        RandomFillMap();
    }

    public void RandomFillMap() {
		foreach (Transform child in transform) {
     		GameObject.Destroy(child.gameObject);
 		}

        for(int x = 0; x < width; x++){
            for(int y = 0; y < height; y++){
                if(x == 0 || x == width - 1 || y == 0 || y == height - 1){
                    CreateVoxel(x, y, true); 
                }
                else{
                    CreateVoxel(x, y, rand.Next(0, 100) < randomFillPercent ? true : false);
                }
            }
        }

		for(int i = 0; i < 5; i++){
			SmoothMap();
		}		
		
		ProcessMap();

		Refresh();
    }

	public void SmoothMap(){
		for(int x = 0; x < width; x++){
			for(int y = 0; y < height; y++){
				int neighbourWallTiles = GetNeighbourWallCount(x, y);

				if(neighbourWallTiles > 4){
					voxels[x, y].state = true;
				}
				else if(neighbourWallTiles < 4){
					voxels[x, y].state = false;
				}
			}
		}
	}

	private int GetNeighbourWallCount(int posX, int posY){
		int wallCount = 0;
		for(int neighbourX = posX - 1; neighbourX <= posX + 1; neighbourX++){
			for(int neighbourY = posY - 1; neighbourY <= posY + 1; neighbourY++){
				if(inRange(neighbourX, neighbourY)){
					if(neighbourX != posX || neighbourY != posY){
						wallCount = voxels[neighbourX, neighbourY].state ? wallCount + 1 : wallCount;
					}
				}
				else{
					wallCount++;
				}
			}
		}

		return wallCount;
	}

	void ProcessMap(){
		List<List<Coord>> wallRegions = GetRegions(true);
		int wallThresholdSize = 50;

		foreach(List<Coord> wallRegion in wallRegions){	
			if(wallRegion.Count < wallThresholdSize){
				
				foreach(Coord tile in wallRegion){
					voxels[tile.tileX, tile.tileY].state = false;
				}
			}
		}

		List<List<Coord>> emptyRegions = GetRegions(false);
		int emptyThresholdSize = 50;
		List<Room> survivingRooms = new List<Room>();

		foreach(List<Coord> emptyRegion in emptyRegions){	
			if(emptyRegion.Count < emptyThresholdSize){
				foreach(Coord tile in emptyRegion){
					voxels[tile.tileX, tile.tileY].state = true;
				}
			}
			else {
				survivingRooms.Add(new Room(emptyRegion, voxels));
			}
		}

		survivingRooms.Sort();
		survivingRooms[0].isMain = true;
		survivingRooms[0].connectedToMain = true;

		ConnectClosestRooms(survivingRooms);
	}
	void ConnectClosestRooms(List<Room> allRooms, bool forceAccessibilityFromMainRoom = false) {
		List<Room> roomListA = new List<Room> ();
		List<Room> roomListB = new List<Room> ();

		if (forceAccessibilityFromMainRoom) {
			foreach (Room room in allRooms) {
				if (room.connectedToMain) {
					roomListB.Add (room);
				} else {
					roomListA.Add (room);
				}
			}
		} else {
			roomListA = allRooms;
			roomListB = allRooms;
		}

		int bestDistance = 0;
		Coord bestTileA = new Coord();
		Coord bestTileB = new Coord();
		Room bestRoomA = new Room();
		Room bestRoomB = new Room();
		bool possibleConnectionFound = false;

		foreach (Room roomA in roomListA) {
			if (!forceAccessibilityFromMainRoom) {
				possibleConnectionFound = false;
				if (roomA.connectedRooms.Count > 0) {
					continue;
				}
			}

			foreach (Room roomB in roomListB) {
				if (roomA == roomB || roomA.IsConnected(roomB)) {
					continue;
				}

				for (int tileIndexA = 0; tileIndexA < roomA.edgeTiles.Count; tileIndexA ++) {
					for (int tileIndexB = 0; tileIndexB < roomB.edgeTiles.Count; tileIndexB ++) {
						Coord tileA = roomA.edgeTiles[tileIndexA];
						Coord tileB = roomB.edgeTiles[tileIndexB];
						int distanceBetweenRooms = (int)((tileA.tileX-tileB.tileX)*(tileA.tileX-tileB.tileX) + (tileA.tileY-tileB.tileY) * (tileA.tileY-tileB.tileY));

						if (distanceBetweenRooms < bestDistance || !possibleConnectionFound) {
							bestDistance = distanceBetweenRooms;
							possibleConnectionFound = true;
							bestTileA = tileA;
							bestTileB = tileB;
							bestRoomA = roomA;
							bestRoomB = roomB;
						}
					}
				}	
			}

			if (possibleConnectionFound && !forceAccessibilityFromMainRoom) {
					CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			}
		}
		if (possibleConnectionFound && forceAccessibilityFromMainRoom) {
			CreatePassage(bestRoomA, bestRoomB, bestTileA, bestTileB);
			ConnectClosestRooms(allRooms, true);
		}

		if (!forceAccessibilityFromMainRoom) {
			ConnectClosestRooms(allRooms, true);
		}
	}

	void CreatePassage(Room roomA, Room roomB, Coord tileA, Coord tileB) {
		Room.ConnectRooms (roomA, roomB);

		Vector2 currentPos = new Vector2(tileA.tileX, tileA.tileY);
		Coord tileC;

		Vector2 direction = new Vector2(tileB.tileX - tileA.tileX, tileB.tileY - tileA.tileY);
		direction.Normalize();

		if (connectRooms)
		{
			while (Mathf.Abs(currentPos.x - tileB.tileX) > 1 | Mathf.Abs(currentPos.y - tileB.tileY) > 1)
			{
				currentPos += direction;
				tileC = new Coord((int)Mathf.Round(currentPos.x), (int)Mathf.Round(currentPos.y));

				voxels[tileC.tileX, tileC.tileY - 1].state = false;
				voxels[tileC.tileX, tileC.tileY].state = false;
				voxels[tileC.tileX, tileC.tileY + 1].state = false;
				voxels[tileC.tileX - 1, tileC.tileY - 1].state = false;
				voxels[tileC.tileX - 1, tileC.tileY].state = false;
				voxels[tileC.tileX - 1, tileC.tileY + 1].state = false;
				voxels[tileC.tileX + 1, tileC.tileY - 1].state = false;
				voxels[tileC.tileX + 1, tileC.tileY].state = false;
				voxels[tileC.tileX + 1, tileC.tileY + 1].state = false;
			}
		}
	}


	List<List<Coord>> GetRegions(bool isWall){
		List<List<Coord>> tileRegions = new List<List<Coord>>();
		bool[,] floodFilled = new bool[width, height];

		for(int x = 0; x < width; x++){
			for(int y = 0; y < height; y++){
				if(!floodFilled[x, y] && voxels[x, y].state == isWall){
					List<Coord> newRegion = GetRegionTiles(x, y);
					tileRegions.Add(newRegion);

					foreach(Coord tile in newRegion){
						floodFilled[tile.tileX, tile.tileY] = true;
					}
				}	
			}
		}

		return tileRegions;
	}

	List<Coord> GetRegionTiles(int startX, int startY){
		List<Coord> tiles = new List<Coord>();
		bool[,] floodFilled = new bool[width, height];
		bool isWall = voxels[startX, startY].state;

		Queue<Coord> floodQueue = new Queue<Coord>();
		floodQueue.Enqueue(new Coord(startX, startY));
		floodFilled[startX, startY] = true;

		while(floodQueue.Count > 0){
			Coord tile = floodQueue.Dequeue();
			tiles.Add(tile);

			for(int x = tile.tileX - 1; x <= tile.tileX + 1; x++){
				for(int y = tile.tileY - 1; y <= tile.tileY + 1; y++){
					if(inRange(x, y) && (x == tile.tileX || y == tile.tileY)){
						if(!floodFilled[x, y] && voxels[x, y].state == isWall){
							floodFilled[x, y] = true;
							floodQueue.Enqueue(new Coord(x, y));	
							
						}
					}	
				}
			}
		}
		return tiles;
	}


	private bool inRange(int posX, int posY){
		return posX >= 0 && posX < width && posY >= 0 && posY < height;
	}
    private void CreateVoxel (int x, int y, bool state) {
		GameObject o = Instantiate(voxelPrefab) as GameObject;
		o.transform.parent = transform;
		o.transform.localPosition = new Vector3((x + 0.5f) * voxelSize, (y + 0.5f) * voxelSize, -0.01f);
		o.transform.localScale = Vector3.one * voxelSize * 0.1f;
        voxelMaterials[x, y] = o.GetComponent<MeshRenderer>().material;
        voxels[x, y] = new Voxel(x, y, voxelSize, state);
	}
    
    private void SetVoxelColors () {
		for (int x = 0; x < width; x++) {
            for(int y = 0; y < height; y++){
			    voxelMaterials[x, y].color = voxels[x, y].state ? Color.black : Color.white;
            }
		}
	}

    public void Refresh () {
		SetVoxelColors();
		Triangulate();
	}

	private void Triangulate () {
		vertices.Clear();
		triangles.Clear();
		mesh.Clear();

		TriangulateCellRows();

		mesh.vertices = vertices.ToArray();
		mesh.triangles = triangles.ToArray();
	}

    private void TriangulateCellRows () {
		for (int x = 0; x < width - 1; x++) {
			for (int y = 0; y < height - 1; y++) {
				TriangulateCell(
                    voxels[x, y],
                    voxels[x + 1, y],
                    voxels[x, y + 1],
                    voxels[x + 1, y + 1]
                );
			}
		}
	}
    private void TriangulateCell (Voxel a, Voxel b, Voxel c, Voxel d) {
        int cellType = 0;

		if (a.state) {
			cellType |= 1;
		}
		if (b.state) {
			cellType |= 2;
		}
		if (c.state) {
			cellType |= 4;
		}
		if (d.state) {
			cellType |= 8;
		}

        switch (cellType) {
		case 0:
			return;
        case 1:
			AddTriangle(a.position, a.yEdgePosition, a.xEdgePosition);
			break;
        case 2:
			AddTriangle(b.position, a.xEdgePosition, b.yEdgePosition);
			break;
		case 4:
			AddTriangle(c.position, c.xEdgePosition, a.yEdgePosition);
			break;
		case 8:
			AddTriangle(d.position, b.yEdgePosition, c.xEdgePosition);
			break;
        case 3:
			AddQuad(a.position, a.yEdgePosition, b.yEdgePosition, b.position);
			break;
		case 5:
			AddQuad(a.position, c.position, c.xEdgePosition, a.xEdgePosition);
			break;
		case 10:
			AddQuad(a.xEdgePosition, c.xEdgePosition, d.position, b.position);
			break;
		case 12:
			AddQuad(a.yEdgePosition, c.position, d.position, b.yEdgePosition);
			break;
		case 15:
			AddQuad(a.position, c.position, d.position, b.position);
			break;
        case 7:
			AddPentagon(a.position, c.position, c.xEdgePosition, b.yEdgePosition, b.position);
			break;
		case 11:
			AddPentagon(b.position, a.position, a.yEdgePosition, c.xEdgePosition, d.position);
			break;
		case 13:
			AddPentagon(c.position, d.position, b.yEdgePosition, a.xEdgePosition, a.position);
			break;
		case 14:
			AddPentagon(d.position, b.position, a.xEdgePosition, a.yEdgePosition, c.position);
			break;
        case 6:
			AddTriangle(b.position, a.xEdgePosition, b.yEdgePosition);
			AddTriangle(c.position, c.xEdgePosition, a.yEdgePosition);
			break;
		case 9:
			AddTriangle(a.position, a.yEdgePosition, a.xEdgePosition);
			AddTriangle(d.position, b.yEdgePosition, c.xEdgePosition);
			break;
		}
	}

    private void AddTriangle (Vector3 a, Vector3 b, Vector3 c) {
		int vertexIndex = vertices.Count;
		vertices.Add(a);
		vertices.Add(b);
		vertices.Add(c);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
	}

    private void AddQuad (Vector3 a, Vector3 b, Vector3 c, Vector3 d) {
		int vertexIndex = vertices.Count;
		vertices.Add(a);
		vertices.Add(b);
		vertices.Add(c);
		vertices.Add(d);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex + 3);
	}

    private void AddPentagon (Vector3 a, Vector3 b, Vector3 c, Vector3 d, Vector3 e) {
		int vertexIndex = vertices.Count;
		vertices.Add(a);
		vertices.Add(b);
		vertices.Add(c);
		vertices.Add(d);
		vertices.Add(e);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 1);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 2);
		triangles.Add(vertexIndex + 3);
		triangles.Add(vertexIndex);
		triangles.Add(vertexIndex + 3);
		triangles.Add(vertexIndex + 4);
	}
}
