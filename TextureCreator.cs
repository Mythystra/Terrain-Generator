using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class TextureCreator : MonoBehaviour
{

	private Texture2D texture;

	private Mesh mesh;
	private Vector3[] vertices;
	private Vector3[] normals;
	private Color[] colors;

	private bool mapGenerated = false;

	public int updatePasses = 1;
	public int resolution;
	public int plateCount;
	public Gradient colouring;
	public bool displayPlates;
	public float roughness;
	public float seedValue;

	[Range( 1, 10 )]
	public int maxMagmaSpreaders;

	[Range( 0.01f, 1f )]
	public float maxElevationIncrease;

	[Range( 1, 10 )]
	public int maxVelocity;

	public List<Plate> plates = new List<Plate>();
	public SquareTile[,] map;
	public SquareTile[,] bufferMap;
	public Collision[,] collisionMap;

	public Vector3 offset;
	public Vector3 rotation;

	private void OnEnable()
	{
		if( resolution % 2 == 0 ) //resolution cannot be even
		{
			resolution++;
		}

		if( texture == null )
		{
			texture = new Texture2D( resolution, resolution, TextureFormat.RGB24, true );
			texture.name = "Heightmap Texture";
			texture.wrapMode = TextureWrapMode.Clamp;
			texture.filterMode = FilterMode.Point;
			texture.anisoLevel = 9;
			GetComponent<MeshRenderer>().material.mainTexture = texture;
		}
		InitializeMap();
		CreateHeightmap( seedValue, roughness );
		CreatePlates();
		GrowPlateFloodFill();
		mapGenerated = true;
	}

	private void Update()
	{
		if( Input.GetKeyDown( KeyCode.Escape ) )
		{
			Application.Quit();
		}
	}

	public void TileUpdate()
	{
		bufferMap = new SquareTile[resolution, resolution]; //clear array
		collisionMap = new Collision[resolution, resolution];
		//for( int x = 0; x < resolution; x++ )
		//{
		//	for( int y = 0; y < resolution; y++ )
		//	{
		//		collisionMap[x, y] = new Collision();
		//	}
		//}

		for( int i = 0; i < plates.Count; i++ )
		{
			for( int j = 0; j < plates[i].tileList.Count; j++ )
			{
				plates[i].tileList[j].Coordinates += plates[i].Velocity;
				plates[i].tileList[j].Age++;

				if( plates[i].tileList[j].Coordinates.x >= resolution )
				{
					Vector2 newCoords = new Vector2( plates[i].tileList[j].Coordinates.x - resolution, plates[i].tileList[j].Coordinates.y );
					plates[i].tileList[j].Coordinates = newCoords;
				}
				if( plates[i].tileList[j].Coordinates.x < 0 )
				{
					Vector2 newCoords = new Vector2( plates[i].tileList[j].Coordinates.x + resolution, plates[i].tileList[j].Coordinates.y );
					plates[i].tileList[j].Coordinates = newCoords;
				}

				if( plates[i].tileList[j].Coordinates.y >= resolution )
				{
					Vector2 newCoords = new Vector2( plates[i].tileList[j].Coordinates.x, plates[i].tileList[j].Coordinates.y - resolution );
					plates[i].tileList[j].Coordinates = newCoords;
				}
				if( plates[i].tileList[j].Coordinates.y < 0 )
				{
					Vector2 newCoords = new Vector2( plates[i].tileList[j].Coordinates.x, plates[i].tileList[j].Coordinates.y + resolution );
					plates[i].tileList[j].Coordinates = newCoords;
				}
				if( collisionMap[(int)plates[i].tileList[j].Coordinates.x, (int)plates[i].tileList[j].Coordinates.y] == null )
				{
					collisionMap[(int)plates[i].tileList[j].Coordinates.x, (int)plates[i].tileList[j].Coordinates.y] = new Collision();
				}
				collisionMap[(int)plates[i].tileList[j].Coordinates.x, (int)plates[i].tileList[j].Coordinates.y].collisionList.Add( plates[i].tileList[j] ); //add to collision map
			}
			plates[i].RecalculateDensity();
		}

		//RESOLVE COLLISIONS
		for( int x = 0; x < resolution; x++ )
		{
			for( int y = 0; y < resolution; y++ )
			{
				if( collisionMap[x, y] == null )
				{
					continue;
				}
				if( collisionMap[x, y].collisionList.Count > 1 )
				{
					int index = collisionMap[x, y].collisionList.Count;
					collisionMap[x, y].collisionList.Sort( SortByDensity );
					bufferMap[x, y] = collisionMap[x, y].collisionList[index - 1];
					collisionMap[x, y].collisionList[index - 1].ParentPlate.Magma++;

					//int indexOfHighest = 0;

					//for( int i = 0; i < collisionMap[x, y].collisionList.Count; i++ )
					//{
					//	float highestDensity = 0;
					//	if( collisionMap[x, y].collisionList[i].ParentPlate.Density > highestDensity )
					//	{
					//		highestDensity = collisionMap[x, y].collisionList[i].ParentPlate.Density;
					//		indexOfHighest = i;
					//	}
					//}
					//bufferMap[x, y] = collisionMap[x, y].collisionList[indexOfHighest];
					//collisionMap[x, y].collisionList[indexOfHighest].ParentPlate.Magma += collisionMap[x, y].collisionList.Count - 1;

				}
				else
				{
					bufferMap[x, y] = collisionMap[x, y].collisionList[0];
				}
			}
		}
		Debug.Log( "Resolve collisions and generate magma: Complete" );

		SpreadMagma(); //spread magma on plates
		Debug.Log( "Magma Spread: Complete" );

		//Divergent - fill empty spaces with new TILE
		for( int y = 0; y < resolution; y++ )
		{
			for( int x = 0; x < resolution; x++ )
			{
				if( bufferMap[x, y] == null )
				{
					//bufferMap[x, y] = new SquareTile(); //add to adjacent plate
					DivergentFloodFill( x, y );
				}
			}
		}

		Debug.Log( "Divergent FloodFill: Complete" );

		map = bufferMap; //swap over resolved map
		Debug.Log( "Swap over map and buffer map: Complete" );
	}

	private void SpreadMagma()
	{
		foreach( Plate plate in plates )
		{
			if( plate.Magma > 0 )
			{
				//choose one or multiple magma spreading points
				int numberOfSpreaders = Random.Range( 1, maxMagmaSpreaders );
				List<Vector2> Spreaders = new List<Vector2>();

				for( int i = 0; i < numberOfSpreaders; i++ )
				{
					int random = Random.Range( 1, plate.tileList.Count );
					Spreaders.Add( plate.tileList[random].Coordinates );
				}

				int dividedMagma = Mathf.RoundToInt( plate.Magma / numberOfSpreaders );
				plate.Magma = 0;

				foreach( Vector2 spreader in Spreaders )
				{
					MagmaFlow( spreader, dividedMagma );
				}
			}
		}
	}

	private void MagmaFlow( Vector2 lStartPoint, int lMagma )
	{
		bufferMap[(int)lStartPoint.x, (int)lStartPoint.y].Elevation += maxElevationIncrease;
		lMagma--;
		Vector2 lCurrentPoint = lStartPoint;

		List<Vector2> possibleNewPoints = new List<Vector2>();
		while( lMagma > 0 )
		{
			for( int x = -1; x < 2; x++ )
			{
				for( int y = -1; y < 2; y++ )
				{
					int resolutionModX = 0;
					int resolutionModY = 0;

					if( (int)lCurrentPoint.x + x >= resolution )
					{
						resolutionModX = resolution;
					}
					if( (int)lCurrentPoint.y + y >= resolution )
					{
						resolutionModY = resolution;
					}
					if( (int)lCurrentPoint.x + x < 0 )
					{
						resolutionModX = -resolution;
					}
					if( (int)lCurrentPoint.y + y < 0 )
					{
						resolutionModY = -resolution;
					}
					if( bufferMap[(int)lCurrentPoint.x + x - resolutionModX, (int)lCurrentPoint.y + y - resolutionModY] != null )
					{
						if( bufferMap[(int)lCurrentPoint.x, (int)lCurrentPoint.y].Elevation > bufferMap[(int)lCurrentPoint.x + x - resolutionModX, (int)lCurrentPoint.y + y - resolutionModY].Elevation )
						{
							possibleNewPoints.Add( new Vector2( lCurrentPoint.x + x - resolutionModX, lCurrentPoint.y + y - resolutionModY ) );
						}
					}
				}
			}
			if( possibleNewPoints.Count == 0 )
			{
				bufferMap[(int)lCurrentPoint.x, (int)lCurrentPoint.y].Elevation += maxElevationIncrease;
				lMagma--;
			}
			else
			{
				lCurrentPoint = possibleNewPoints[Random.Range( 0, possibleNewPoints.Count - 1 )];
				bufferMap[(int)lCurrentPoint.x, (int)lCurrentPoint.y].Elevation += maxElevationIncrease;
				lMagma--;
				possibleNewPoints.Clear();
			}
		}
	}

	private void DivergentFloodFill( int lx, int ly )
	{
		Queue<Vector2> floodQueue = new Queue<Vector2>();
		List<Vector2> closedList = new List<Vector2>();
		floodQueue.Enqueue( new Vector2( lx, ly ) );

		while( floodQueue.Count > 0 )
		{
			Vector2 currentTile = floodQueue.Dequeue();
			//store x and y
			closedList.Add( currentTile );
			for( int x = -1; x < 2; x++ )
			{
				for( int y = -1; y < 2; y++ )
				{
					int resolutionModX = 0;
					int resolutionModY = 0;

					if( (int)currentTile.x + x >= resolution )
					{
						resolutionModX = resolution;
					}
					if( (int)currentTile.y + y >= resolution )
					{
						resolutionModY = resolution;
					}
					if( (int)currentTile.x + x < 0 )
					{
						resolutionModX = -resolution;
					}
					if( (int)currentTile.y + y < 0 )
					{
						resolutionModY = -resolution;
					}
					if( bufferMap[(int)currentTile.x + x - resolutionModX, (int)currentTile.y + y - resolutionModY] == null && !closedList.Contains( new Vector2( currentTile.x + x - resolutionModX, currentTile.y + y - resolutionModY ) ) )
					{
						floodQueue.Enqueue( new Vector2( currentTile.x + x - resolutionModX, currentTile.y + y - resolutionModY ) );
					}
				}
			}
		}
		foreach( Vector2 tile in closedList )
		{
			//SquareTile newTile = new SquareTile( tile, Mathf.RoundToInt( tile.x + ( tile.y * resolution ) - resolution ) );
			SquareTile newTile = new SquareTile( tile );
			newTile.ParentPlate = FindClosestPlate( newTile );
			newTile.ParentPlate.tileList.Add( newTile );
			//newTile.Elevation = Random.Range( 0.01f, 0.3f ); //sea level
			newTile.Elevation = 0.01f;
			bufferMap[(int)tile.x, (int)tile.y] = newTile;
		}
	}

	private Plate FindClosestPlate( SquareTile lTile )
	{
		//THIS IS SHIT REDO
		int lx = 1;
		int ly = 1;
		bool lFound = false;
		List<Plate> closestPlates = new List<Plate>();

		while( !lFound )
		{
			int xPosMod = 0;
			int yPosMod = 0;
			int xNegMod = 0;
			int yNegMod = 0;

			if( lTile.Coordinates.x + lx >= resolution )
			{
				xPosMod = -resolution;
			}
			if( lTile.Coordinates.x - lx < 0 )
			{
				xNegMod = resolution;
			}

			if( lTile.Coordinates.y + ly >= resolution )
			{
				yPosMod = -resolution;
			}
			if( lTile.Coordinates.y - ly < 0 )
			{
				yNegMod = resolution;
			}

			if( bufferMap[(int)lTile.Coordinates.x + lx + xPosMod, (int)lTile.Coordinates.y + ly + yPosMod] != null )
			{
				closestPlates.Add( bufferMap[(int)lTile.Coordinates.x + lx + xPosMod, (int)lTile.Coordinates.y + ly + yPosMod].ParentPlate );
				lFound = true;
			}
			if( bufferMap[(int)lTile.Coordinates.x - lx + xNegMod, (int)lTile.Coordinates.y + ly + yPosMod] != null )
			{
				closestPlates.Add( bufferMap[(int)lTile.Coordinates.x - lx + xNegMod, (int)lTile.Coordinates.y + ly + yPosMod].ParentPlate );
				lFound = true;
			}
			if( bufferMap[(int)lTile.Coordinates.x + lx + xPosMod, (int)lTile.Coordinates.y - ly + yNegMod] != null )
			{
				closestPlates.Add( bufferMap[(int)lTile.Coordinates.x + lx + xPosMod, (int)lTile.Coordinates.y - ly + yNegMod].ParentPlate );
				lFound = true;
			}
			if( bufferMap[(int)lTile.Coordinates.x - lx + xNegMod, (int)lTile.Coordinates.y - ly + yNegMod] != null )
			{
				closestPlates.Add( bufferMap[(int)lTile.Coordinates.x - lx + xNegMod, (int)lTile.Coordinates.y - ly + yNegMod].ParentPlate );
				lFound = true;
			}
			if( lFound == true )
			{
				break;
			}
			lx++;
			ly++;
		}
		return closestPlates[Random.Range( 0, closestPlates.Count )];
	}

	private void InitializeMap()
	{
		map = new SquareTile[resolution, resolution];
		int id = 0;
		for( int y = 0; y < resolution; y++ )
		{
			for( int x = 0; x < resolution; x++ )
			{
				map[x, y] = new SquareTile( new Vector2( x, y ), id );
				map[x, y].Age = Random.Range( 0, 10 ); //age between 0 and 10
				id++;
			}
		}
	}

	private void CheckMapCollisions()
	{

	}

	private void CreateHeightmap( float seed, float roughness )
	{
		map[0, 0].Elevation = map[0, resolution - 1].Elevation = map[resolution - 1, 0].Elevation = map[resolution - 1, resolution - 1].Elevation = seed;
		float h = roughness;

		for( int sideLength = resolution - 1; sideLength >= 2; sideLength /= 2, h /= 2.0f )
		{
			int halfSide = sideLength / 2;
			for( int x = 0; x < resolution - 1; x += sideLength ) //square step
			{
				for( int y = 0; y < resolution - 1; y += sideLength )
				{
					float average = map[x, y].Elevation + //top left
					map[x + sideLength, y].Elevation + //top right
					map[x, y + sideLength].Elevation + //bottom left
					map[x + sideLength, y + sideLength].Elevation;
					average *= 0.25f;

					map[x + halfSide, y + halfSide].Elevation = average + ( ( Random.value * 2 * h ) - h );
				}
			}
			for( int x = 0; x < resolution - 1; x += halfSide ) //diamond step
			{
				for( int y = ( x + halfSide ) % sideLength; y < resolution - 1; y += sideLength )
				{
					float average =
					map[( x - halfSide + resolution ) % resolution, y].Elevation +
					map[( x + halfSide ) % resolution, y].Elevation +
					map[x, ( y + halfSide ) % resolution].Elevation +
					map[x, ( y - halfSide + resolution ) % resolution].Elevation;
					average *= 0.25f;

					average = average + ( ( Random.value * 2 * h ) - h );
					map[x, y].Elevation = average;

					if( x == 0 )
						map[resolution - 1, y].Elevation = average;
					if( y == 0 )
						map[x, resolution - 1].Elevation = average;

				}
			}
		}
	}

	private void CreatePlates()
	{
		List<Vector2> startPoints = new List<Vector2>();
		while( startPoints.Count != plateCount )
		{
			Vector2 newStart = new Vector2( Random.Range( 0, resolution - 1 ), Random.Range( 0, resolution - 1 ) );
			if( startPoints.Count == 0 )
				startPoints.Add( newStart );
			else
			{
				bool exists = false;
				foreach( Vector2 start in startPoints )
				{
					if( start == newStart )
					{
						exists = true;
						break;
					}
					else
						continue;
				}
				if( !exists )
					startPoints.Add( newStart );
			}
		}

		foreach( Vector2 start in startPoints )
		{
			CreatePlate( start, Random.ColorHSV() );
		}
	}

	private void CreatePlate( Vector2 coordinates, Color plateColor ) //Create a plate at a given coordinate
	{
		Plate newPlate = new Plate();

		foreach( SquareTile tile in map )
		{
			if( tile.Coordinates == coordinates )
			{
				tile.ParentPlate = newPlate;
				newPlate.StartTile = tile;
				newPlate.PlateColor = plateColor;
				newPlate.growQueue = new List<SquareTile>();
				newPlate.tileList = new List<SquareTile>();
				newPlate.toChange = new List<SquareTile>();
				//newPlate.Velocity = new Vector2( Random.Range( -maxVelocity, maxVelocity ), Random.Range( -maxVelocity, maxVelocity ) );
				newPlate.Velocity = new Vector2( Random.Range( 1, maxVelocity ), Random.Range( 1, maxVelocity ) );
			}
		}
		plates.Add( newPlate );
	}

	private void GrowPlateFloodFill() //needs bug fixing
	{
		List<int> plateCount = new List<int>();
		int plateCounter = 0;

		foreach( Plate plate in plates )
		{
			plate.growQueue.Add( plate.StartTile );
			plate.tileList.Add( plate.StartTile );
			plateCount.Add( plateCounter );
			plateCounter++;
		}

		while( plateCount.Count != 0 )
		{
			for( int y = -1; y <= 1; y++ )
			{
				for( int x = -1; x <= 1; x++ )
				{
					for( int i = 0; i < plateCount.Count; i++ )
					{
						//check plate growQueue if empty then skip and remove from plateCount
						if( plates[plateCount[i]].growQueue.Count == 0 )
						{
							plateCount.Remove( plateCount[i] );
							i--;
						}
						else
						{
							SquareTile currentTile = plates[plateCount[i]].growQueue[0];
							int xCoordinate = (int)currentTile.Coordinates.x + x;
							int yCoordinate = (int)currentTile.Coordinates.y + y;
							Vector2 tempCoordinates = new Vector2( xCoordinate, yCoordinate );

							if( (int)tempCoordinates.x > resolution - 1 || (int)tempCoordinates.y > resolution - 1 || (int)tempCoordinates.x < 0 || (int)tempCoordinates.y < 0 )
								continue;
							else if( map[(int)tempCoordinates.x, (int)tempCoordinates.y].ParentPlate == null )
							{
								map[(int)tempCoordinates.x, (int)tempCoordinates.y].ParentPlate = currentTile.ParentPlate;
								plates[plateCount[i]].tileList.Add( map[(int)tempCoordinates.x, (int)tempCoordinates.y] ); //add tile to plate's tile list
								plates[plateCount[i]].growQueue.Add( map[(int)tempCoordinates.x, (int)tempCoordinates.y] ); //add tile to plate's grow queue
							}
						}
					}
				}
			}
			foreach( Plate plate in plates )
			{
				if( plate.growQueue.Count == 0 )
					continue;
				plate.growQueue.Remove( plate.growQueue[0] ); //remove first tile in plate's grow queue
			}
		}
	}

	public void FillTexture()
	{
		if( texture.width != resolution )
		{
			texture.Resize( resolution, resolution );
		}

		for( int y = 0; y < resolution; y++ )
		{
			for( int x = 0; x < resolution; x++ )
			{
				SquareTile tile = map[x, y];
				if( tile == null )
					continue;
				if( displayPlates )
				{
					if( tile.ParentPlate == null )
						continue;
					else
						texture.SetPixel( x, y, tile.ParentPlate.PlateColor ); //CHANGE TO DISPLAY COLOUR BASED ON ELEVATION
				}
				else
				{
					texture.SetPixel( x, y, colouring.Evaluate( tile.Elevation ) );
				}
			}
		}
		texture.Apply();
	}

	public int SortByDensity( SquareTile a, SquareTile b )
	{
		if( a.ParentPlate.Density < b.ParentPlate.Density )
		{
			return 1;
		}
		if( b.ParentPlate.Density < a.ParentPlate.Density )
		{
			return -1;
		}
		else
		{
			return 0;
		}
	}

	public void Exit()
	{
		Application.Quit();
	}

	public void Generate3D()
	{
		if( mesh == null )
		{
			mesh = new Mesh();
			mesh.name = "Surface Mesh";
			GetComponent<MeshFilter>().mesh = mesh;
		}
		CreateGrid();
	}

	private void CreateGrid()
	{
		mesh.Clear();

		vertices = new Vector3[( resolution + 1 ) * ( resolution + 1 )];
		colors = new Color[vertices.Length];
		normals = new Vector3[vertices.Length];
		Vector2[] uv = new Vector2[vertices.Length];
		float stepSize = 1f / resolution;
		for( int v = 0, z = 0; z <= resolution; z++ )
		{
			for( int x = 0; x <= resolution; x++, v++ )
			{
				vertices[v] = new Vector3( x * stepSize - 0.5f, 0f, z * stepSize - 0.5f );
				colors[v] = Color.black;
				normals[v] = Vector3.up;
				uv[v] = new Vector2( x * stepSize, z * stepSize );
			}
		}
		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.normals = normals;
		mesh.uv = uv;

		int[] triangles = new int[resolution * resolution * 6];
		for( int t = 0, v = 0, y = 0; y < resolution; y++, v++ )
		{
			for( int x = 0; x < resolution; x++, v++, t += 6 )
			{
				triangles[t] = v;
				triangles[t + 1] = v + resolution + 1;
				triangles[t + 2] = v + 1;
				triangles[t + 3] = v + 1;
				triangles[t + 4] = v + resolution + 1;
				triangles[t + 5] = v + resolution + 2;
			}
		}
		mesh.triangles = triangles;
		RefreshGrid();
	}

	private void RefreshGrid()
	{
		for( int v = 0, y = 0; y < resolution; y++ )
		{
			for( int x = 0; x < resolution; x++, v++ )
			{
				vertices[v].y = map[y, x].Elevation;
				colors[v] = colouring.Evaluate( vertices[v].y );
			}
		}
		mesh.vertices = vertices;
		mesh.colors = colors;
		mesh.RecalculateNormals();
		GetComponent<MeshRenderer>().material.mainTexture = null;
	}
}
