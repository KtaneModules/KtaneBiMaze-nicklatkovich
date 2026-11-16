using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

public class Maze {
	// vanilla uses (0,-1) as up and (0,+1) as down
	public static readonly ReadOnlyCollection<Vector2Int> DD = System.Array.AsReadOnly(new Vector2Int[] { Vector2Int.right, Vector2Int.down, Vector2Int.left, Vector2Int.up });
	public const int DIRECTION_RIGHT = 0;
	public const int DIRECTION_UP = 1;
	public const int DIRECTION_LEFT = 2;
	public const int DIRECTION_DOWN = 3;

	public class Cell {
		public Vector2Int Position;
		public bool[] Walls = Enumerable.Repeat(true, 4).ToArray();
		public Cell(Vector2Int pos) { Position = pos; }
	}

	public static readonly Vector2Int SIZE = new Vector2Int(6, 6);
	public static readonly int TOTAL_CELLS = SIZE.x * SIZE.y;
	public static bool IsInMaze(Vector2Int pos) { return pos.x >= 0 && pos.x < SIZE.x && pos.y >= 0 && pos.y < SIZE.y; }

	public Cell[][] Cells = Enumerable.Range(0, SIZE.x).Select(x => Enumerable.Range(0, SIZE.y).Select(y => new Cell(new Vector2Int(x, y))).ToArray()).ToArray();
	public List<Vector2Int> Indicators = new List<Vector2Int>();

	public void RemoveWall(Vector2Int pos, int dd) {
		Vector2Int nxt = pos + DD[dd];
		Cells[pos.x][pos.y].Walls[dd] = false;
		Cells[nxt.x][nxt.y].Walls[(dd + 2) % 4] = false;
	}
}
