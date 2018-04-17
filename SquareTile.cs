using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class SquareTile
{

	private Vector2 coordinates;
	private Plate parent;
	private float elevation;
	private int indexInGrid;
	private int age;

	public Vector2 Coordinates
	{
		get
		{
			return coordinates;
		}
		set
		{
			coordinates = value;
		}
	}

	public Plate ParentPlate
	{
		get
		{
			return parent;
		}
		set
		{
			parent = value;
		}
	}

	public float Elevation
	{
		get
		{
			return elevation;
		}
		set
		{
			elevation = value;
		}
	}

	public int IndexInGrid
	{
		get
		{
			return indexInGrid;
		}
		set
		{
			indexInGrid = value;
		}
	}

	public int Age
	{
		get
		{
			return age;
		}
		set
		{
			age = value;
		}
	}

	public SquareTile( Vector2 newCoordinates, int index )
	{
		coordinates = newCoordinates;
		indexInGrid = index;
	}

	public SquareTile( Vector2 newCoordinates )
	{
		coordinates = newCoordinates;
	}

	public SquareTile()
	{

	}
}
