using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Plate
{

	public List<SquareTile> tileList;
	public List<SquareTile> growQueue; //used for tile growth
	public List<SquareTile> toChange;

	private SquareTile startTile;
	private Color plateColor;
	private PlateType m_plateType;
	private float mass; //amount of crust in plate
	private Vector2 velocity; //plate's velocity
	private float density; //density changes with age - find sum of all tiles age and average out
	private float magma;

	public enum PlateType
	{
		Oceanic,
		Continental
	}

	public SquareTile StartTile
	{
		get
		{
			return startTile;
		}
		set
		{
			startTile = value;
		}
	}

	public Color PlateColor
	{
		get
		{
			return plateColor;
		}
		set
		{
			plateColor = value;
		}
	}

	public PlateType plateType
	{
		get
		{
			return m_plateType;
		}
		set
		{
			m_plateType = value;
		}
	}

	public Vector2 Velocity
	{
		get { return velocity; }
		set { velocity = value; }
	}

	//public float Momentum { get { return mass * velocity; } }
	public float Density { get { return density; } }
	public float Magma { get { return magma; } set { magma = value; } }

	public void RecalculateDensity()
	{
		density = 0;
		foreach( SquareTile tile in tileList )
		{
			density += tile.Age;
		}
		density = Mathf.RoundToInt( density / tileList.Count );
	}

	public void SortCoordinates()
	{
		tileList.Sort( SortByCoordinates );
	}

	public int SortByCoordinates( SquareTile a, SquareTile b )
	{
		if( a.Coordinates.y < b.Coordinates.y )
			return -1;

		if( Mathf.Approximately( a.Coordinates.y, b.Coordinates.y ) )
		{
			if( Mathf.Approximately( a.Coordinates.x, b.Coordinates.x ) )
				return 0;
			if( a.Coordinates.x < b.Coordinates.x )
				return -1;
		}
		return 1;
	}
}
