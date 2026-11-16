using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;

using Random = UnityEngine.Random;

public static class BiMazeUtils {
	public const int VANILLA_MAZES_COUNT = 9;
	public const int BIMAZES_OFFSET = 3;
	public const int BIMAZES_COUNT = VANILLA_MAZES_COUNT - BIMAZES_OFFSET;
	public static readonly ReadOnlyCollection<ReadOnlyCollection<Vector2Int>> WALL_CHECKERS = Array.AsReadOnly(new[] {
		Array.AsReadOnly(new [] { new Vector2Int(0, 0), new Vector2Int(0, 1) }),
		Array.AsReadOnly(new [] { new Vector2Int(0, 0), new Vector2Int(-1, 0) }),
		Array.AsReadOnly(new [] { new Vector2Int(-1, 0), new Vector2Int(-1, 1) }),
		Array.AsReadOnly(new [] { new Vector2Int(-1, 1), new Vector2Int(0, 1) }),
	});

	public struct WallRemovalResult {
		public int Direction;
		public int ResultingRoomSize;
		public bool CreatesNonConnectedWall;
		public bool ConnectsTo1CellRoom;
		public bool HasWallInOrigin;
	}

	public struct WallId {
		public Vector2Int Position;
		public int Direction;
		public bool Equals(Vector2Int pos, int dd) { return pos.x == Position.x && pos.y == Position.y && dd == Direction; }
	}

	public static bool IsInMaze(Vector2Int pos) { return pos.x >= 0 && pos.x < Maze.SIZE.x && pos.y >= 0 && pos.y < Maze.SIZE.y; }
	public static int CountWalls(Maze maze, Vector2Int pos) { return maze.Cells[pos.x][pos.y].Walls.Count(v => v); }

	public static Maze GenerateMaze(MonoRandom rnd, List<Vector2Int> mazeIndicators) {
		for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) {
			if (x > 0) rnd.NextDouble();
			if (y > 0) rnd.NextDouble();
		}
		Maze maze = new Maze();
		Vector2Int pos = new Vector2Int(rnd.Next(0, Maze.SIZE.x), rnd.Next(0, Maze.SIZE.y));
		bool[][] visited = Enumerable.Range(0, Maze.SIZE.x).Select(x => Enumerable.Repeat(false, Maze.SIZE.y).ToArray()).ToArray();
		Stack<Vector2Int> cellStack = new Stack<Vector2Int>();
		while (true) {
			visited[pos.x][pos.y] = true;
			List<int> ddCandidates = new List<int>();
			foreach (int candidateDirection in new[] { Maze.DIRECTION_LEFT, Maze.DIRECTION_RIGHT, Maze.DIRECTION_UP, Maze.DIRECTION_DOWN }) {
				Vector2Int nxt = pos + Maze.DD[candidateDirection];
				if (Maze.IsInMaze(nxt) && !visited[nxt.x][nxt.y]) ddCandidates.Add(candidateDirection);
			}
			if (ddCandidates.Count == 0) {
				if (cellStack.Count > 0) pos = cellStack.Pop();
				else break;
			} else {
				int dd = ddCandidates[rnd.Next(0, ddCandidates.Count)];
				maze.RemoveWall(pos, dd);
				cellStack.Push(pos);
				pos += Maze.DD[dd];
			}
		}
		for (int i = 0; i < 2; i++) {
			int ix = rnd.Next(0, mazeIndicators.Count);
			maze.Indicators.Add(mazeIndicators[ix]);
			mazeIndicators.RemoveAt(ix);
		}
		return maze;
	}

	public static Maze[] Generate(MonoRandom rnd) {
		List<Vector2Int> mazeIndicators = Enumerable.Range(0, Maze.SIZE.x).SelectMany(x => Enumerable.Range(0, Maze.SIZE.y).Select(y => new Vector2Int(x, y))).ToList();
		Maze[] origins = Enumerable.Range(0, VANILLA_MAZES_COUNT).Select(_ => GenerateMaze(rnd, mazeIndicators)).ToArray();
		Maze[] bimazes = BuildBiMazes(rnd, origins);
		return bimazes;
	}

	public static Maze[] BuildBiMazes(MonoRandom rnd, Maze[] mazes) {
		int SKIP_MAZES = BIMAZES_OFFSET;
		Maze[] bimazesList = Enumerable.Range(0, VANILLA_MAZES_COUNT).Select(_ => new Maze()).ToArray();
		for (int i = 0; i < bimazesList.Length; i++) {
			if (i < SKIP_MAZES) {
				bimazesList[i] = mazes[i];
				continue;
			}
			Maze bimaze = bimazesList[i];
			Maze origin = mazes[i];
			bimaze.Indicators = origin.Indicators;
			Vector2Int[] posList = Enumerable.Range(0, Maze.SIZE.x * Maze.SIZE.y).Select(j => new Vector2Int(j % Maze.SIZE.x, j / Maze.SIZE.x)).ToArray();
			rnd.ShuffleFisherYates(posList);
			foreach (Vector2Int pos in posList) {
				Maze.Cell cell = bimaze.Cells[pos.x][pos.y];
				Maze.Cell originCell = origin.Cells[pos.x][pos.y];
				if (cell.Walls.Any(wall => !wall)) continue;
				List<int> dds = new List<int>();
				if (pos.x < 5) dds.Add(Maze.DIRECTION_RIGHT);
				if (pos.y > 0) dds.Add(Maze.DIRECTION_UP);
				if (pos.x > 0) dds.Add(Maze.DIRECTION_LEFT);
				if (pos.y < 5) dds.Add(Maze.DIRECTION_DOWN);
				List<WallRemovalResult> candidates = dds.Select(_dd => CalculateWallRemovalResult(bimaze, pos, _dd, origin)).ToList();
				if (candidates.Any(c => !c.HasWallInOrigin)) candidates = candidates.Where(c => !c.HasWallInOrigin).ToList();
				if (candidates.Any(c => c.ConnectsTo1CellRoom)) candidates = candidates.Where(c => c.ConnectsTo1CellRoom).ToList();
				if (candidates.Any(c => !c.CreatesNonConnectedWall)) candidates = candidates.Where(c => !c.CreatesNonConnectedWall).ToList();
				int minResultingRoomSize = candidates.Min(c => c.ResultingRoomSize);
				candidates = candidates.Where(c => c.ResultingRoomSize == minResultingRoomSize).ToList();
				int ind = rnd.Next(0, candidates.Count);
				int dd = candidates[ind].Direction;
				cell.Walls[dd] = false;
				Vector2Int next = pos + Maze.DD[dd];
				bimaze.Cells[next.x][next.y].Walls[(dd + 2) % 4] = false;
			}
		}
		int[] innerWallsCount = bimazesList.Select(m => CountTotalInnerWalls(m)).ToArray();
		int[] bimazePairsList = Enumerable.Range(0, BIMAZES_COUNT * BIMAZES_COUNT).ToArray();
		rnd.ShuffleFisherYates(bimazePairsList);
		foreach (int pairId in bimazePairsList) {
			int ai = pairId % Maze.SIZE.x + BIMAZES_OFFSET;
			int bi = pairId / Maze.SIZE.x + BIMAZES_OFFSET;
			if (bi <= ai) continue;
			bool[][] visitedCells = Enumerable.Range(0, Maze.SIZE.x).Select(_ => Enumerable.Repeat(false, Maze.SIZE.y).ToArray()).ToArray();
			Vector2Int start = new Vector2Int(rnd.Next(0, Maze.SIZE.x), rnd.Next(0, Maze.SIZE.y));
			visitedCells[start.x][start.y] = true;
			List<Vector2Int> q = new List<Vector2Int> { start };
			while (true) {
				int visitedCellsCount = TryTraverse(bimazesList[ai], bimazesList[bi], rnd, q, visitedCells);
				if (visitedCellsCount == Maze.TOTAL_CELLS) break;
				List<WallCandidateToRemove> wallCandidatesToRemoveList = new List<WallCandidateToRemove>();
				for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) for (int dd = 0; dd < 4; dd++) {
					Vector2Int pos = new Vector2Int(x, y);
					Vector2Int adj = pos + Maze.DD[dd];
					if (!IsInMaze(adj)) continue;
					if (!visitedCells[pos.x][pos.y]) continue;
					if (visitedCells[adj.x][adj.y]) continue;
					wallCandidatesToRemoveList.Add(new WallCandidateToRemove { Position = pos, Direction = dd, MazeIndex = ai });
					wallCandidatesToRemoveList.Add(new WallCandidateToRemove { Position = pos, Direction = dd, MazeIndex = bi });
				}
				WallCandidateToRemove[] wallCandidatesToRemove = wallCandidatesToRemoveList.ToArray();
				WallRemovalResult[] wallRemovalResults = wallCandidatesToRemove.Select(
					e => CalculateWallRemovalResult(bimazesList[e.MazeIndex], e.Position, e.Direction, mazes[e.MazeIndex])
				).ToArray();
				List<int> wallCandidatesToRemoveIndices = Enumerable.Range(0, wallCandidatesToRemove.Length).ToList();
				if (wallCandidatesToRemoveIndices.Any(i => !wallRemovalResults[i].HasWallInOrigin)) {
					wallCandidatesToRemoveIndices = wallCandidatesToRemoveIndices.Where(i => !wallRemovalResults[i].HasWallInOrigin).ToList();
				}
				if (wallCandidatesToRemoveIndices.Any(i => wallRemovalResults[i].ConnectsTo1CellRoom)) {
					wallCandidatesToRemoveIndices = wallCandidatesToRemoveIndices.Where(i => wallRemovalResults[i].ConnectsTo1CellRoom).ToList();
				}
				if (wallCandidatesToRemoveIndices.Any(i => !wallRemovalResults[i].CreatesNonConnectedWall)) {
					wallCandidatesToRemoveIndices = wallCandidatesToRemoveIndices.Where(i => !wallRemovalResults[i].CreatesNonConnectedWall).ToList();
				}
				int minResultingRoomSize = wallCandidatesToRemoveIndices.Min(i => wallRemovalResults[i].ResultingRoomSize);
				wallCandidatesToRemoveIndices = wallCandidatesToRemoveIndices.Where(i => wallRemovalResults[i].ResultingRoomSize == minResultingRoomSize).ToList();
				int maxInnerWallsCount = wallCandidatesToRemoveIndices.Max(i => innerWallsCount[wallCandidatesToRemove[i].MazeIndex]);
				wallCandidatesToRemoveIndices = wallCandidatesToRemoveIndices.Where(i => innerWallsCount[wallCandidatesToRemove[i].MazeIndex] == maxInnerWallsCount).ToList();
				WallCandidateToRemove rm = wallCandidatesToRemove[wallCandidatesToRemoveIndices[rnd.Next(0, wallCandidatesToRemoveIndices.Count)]];
				bimazesList[rm.MazeIndex].Cells[rm.Position.x][rm.Position.y].Walls[rm.Direction] = false;
				Vector2Int next = rm.Position + Maze.DD[rm.Direction];
				bimazesList[rm.MazeIndex].Cells[next.x][next.y].Walls[(rm.Direction + 2) % 4] = false;
				innerWallsCount[rm.MazeIndex] -= 1;
				q.Add(rm.Position);
			}
		}
		List<WallCandidateToAdd> wallCandidatesToAdd = new List<WallCandidateToAdd>();
		for (int mazeIndex = BIMAZES_OFFSET; mazeIndex < VANILLA_MAZES_COUNT; mazeIndex++) {
			Maze bimaze = bimazesList[mazeIndex];
			for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) {
				Vector2Int pos = new Vector2Int(x, y);
				if (x < 5 && !bimaze.Cells[x][y].Walls[Maze.DIRECTION_RIGHT] && CountWalls(bimaze, pos) < 3 && CountWalls(bimaze, pos + Maze.DD[Maze.DIRECTION_RIGHT]) < 3) {
					int roomSize = CalculateRoomSize(bimaze, pos, null);
					wallCandidatesToAdd.Add(new WallCandidateToAdd { MazeIndex = mazeIndex, Position = pos, Direction = Maze.DIRECTION_RIGHT, RoomSize = roomSize });
				}
				if (y < 5 && !bimaze.Cells[x][y].Walls[Maze.DIRECTION_DOWN] && CountWalls(bimaze, pos) < 3 && CountWalls(bimaze, pos + Maze.DD[Maze.DIRECTION_DOWN]) < 3) {
					int roomSize = CalculateRoomSize(bimaze, pos, null);
					wallCandidatesToAdd.Add(new WallCandidateToAdd { MazeIndex = mazeIndex, Position = pos, Direction = Maze.DIRECTION_DOWN, RoomSize = roomSize });
				}
			}
		}
		while (wallCandidatesToAdd.Count > 0) {
			int maxRoomSize = wallCandidatesToAdd.Max(e => e.RoomSize);
			List<int> candidateIndices = Enumerable.Range(0, wallCandidatesToAdd.Count).ToList();
			candidateIndices = candidateIndices.Where(i => wallCandidatesToAdd[i].RoomSize == maxRoomSize).ToList();
			int minWallsCount = candidateIndices.Min(i => innerWallsCount[wallCandidatesToAdd[i].MazeIndex]);
			candidateIndices = candidateIndices.Where(i => innerWallsCount[wallCandidatesToAdd[i].MazeIndex] == minWallsCount).ToList();
			int candidateIndex = candidateIndices[rnd.Next(0, candidateIndices.Count)];
			WallCandidateToAdd rndCandidate = wallCandidatesToAdd[candidateIndex];
			int recalculatedRoomSize = CalculateRoomSize(bimazesList[rndCandidate.MazeIndex], rndCandidate.Position, null);
			if (recalculatedRoomSize != maxRoomSize) {
				rndCandidate.RoomSize = recalculatedRoomSize;
				wallCandidatesToAdd[candidateIndex] = rndCandidate;
				continue;
			}
			Maze bimaze = bimazesList[rndCandidate.MazeIndex];
			Vector2Int next = rndCandidate.Position + Maze.DD[rndCandidate.Direction];
			if (CountWalls(bimaze, rndCandidate.Position) < 3 && CountWalls(bimaze, next) < 3) {
				bimaze.Cells[rndCandidate.Position.x][rndCandidate.Position.y].Walls[rndCandidate.Direction] = true;
				bimaze.Cells[next.x][next.y].Walls[(rndCandidate.Direction + 2) % 4] = true;
				innerWallsCount[rndCandidate.MazeIndex] += 1;
				int[] bimazeIndexesToCheck = Enumerable.Range(BIMAZES_OFFSET, BIMAZES_COUNT).Where(
					i => i != rndCandidate.MazeIndex && bimazesList[i].Cells[rndCandidate.Position.x][rndCandidate.Position.y].Walls[rndCandidate.Direction]
				).ToArray();
				bool remove = false;
				foreach (int otherMazeIndex in bimazeIndexesToCheck) {
					if (TryTraverse(bimaze, bimazesList[otherMazeIndex], rnd) == 36) continue;
					remove = true;
					break;
				}
				if (remove) {
					bimaze.Cells[rndCandidate.Position.x][rndCandidate.Position.y].Walls[rndCandidate.Direction] = false;
					bimaze.Cells[next.x][next.y].Walls[(rndCandidate.Direction + 2) % 4] = false;
					innerWallsCount[rndCandidate.MazeIndex] -= 1;
				}
			}
			int lastInd = wallCandidatesToAdd.Count - 1;
			wallCandidatesToAdd[candidateIndex] = wallCandidatesToAdd[lastInd];
			wallCandidatesToAdd.RemoveAt(lastInd);
		}
		return bimazesList;
	}

	public static int CompareIndicators(Vector2Int ind1, Vector2Int ind2) {
		if (ind1.y != ind2.y) return ind1.y < ind2.y ? -1 : 1;
		return ind1.x < ind2.x ? -1 : 1;
	}

	public static Vector2Int GetFinishPosition(Maze mazeA, Maze mazeB, Vector2Int startPos) {
		Vector2Int finish;
		bool[][] visited = Enumerable.Range(0, Maze.SIZE.x).Select(x => Enumerable.Repeat(false, Maze.SIZE.y).ToArray()).ToArray();
		List<Vector2Int> q = new List<Vector2Int> { startPos };
		visited[startPos.x][startPos.y] = true;
		do {
			finish = q[Random.Range(0, q.Count)];
			List<Vector2Int> nextQ = new List<Vector2Int>();
			foreach (Vector2Int pos in q) for (int dd = 0; dd < 4; dd++) {
				if (mazeA.Cells[pos.x][pos.y].Walls[dd] && mazeB.Cells[pos.x][pos.y].Walls[dd]) continue;
				Vector2Int next = pos + Maze.DD[dd];
				if (!IsInMaze(next)) continue;
				if (visited[next.x][next.y]) continue;
				visited[next.x][next.y] = true;
				nextQ.Add(next);
			}
			q = nextQ;
		} while (q.Count > 0);
		return finish;
	}

	public static WallRemovalResult CalculateWallRemovalResult(Maze bimaze, Vector2Int pos, int direction, Maze origin) {
		Vector2Int next = pos + Maze.DD[direction];
		return new WallRemovalResult {
			Direction = direction,
			ResultingRoomSize = CalculateRoomSize(bimaze, pos, new WallId { Position = pos, Direction = direction }),
			CreatesNonConnectedWall = WALL_CHECKERS[direction].Any(dPos => CountWallsAroundCellCross(bimaze, pos + dPos) <= 2),
			ConnectsTo1CellRoom = IsInMaze(next) && CountWalls(bimaze, next) == 4,
			HasWallInOrigin = origin.Cells[pos.x][pos.y].Walls[direction]
		};
	}

	public static int CalculateRoomSize(Maze maze, Vector2Int from, WallId? ignoreWall) {
		Queue<Vector2Int> q = new Queue<Vector2Int>();
		q.Enqueue(from);
		bool[][] v = Enumerable.Range(0, Maze.SIZE.x).Select(_ => Enumerable.Repeat(false, Maze.SIZE.y).ToArray()).ToArray();
		v[from.x][from.y] = true;
		int result = 1;
		while (q.Count > 0) {
			Vector2Int pos = q.Dequeue();
			for (int dd = 0; dd < 4; dd++) {
				if (maze.Cells[pos.x][pos.y].Walls[dd] && (!ignoreWall.HasValue || !ignoreWall.Value.Equals(pos, dd))) continue;
				Vector2Int next = pos + Maze.DD[dd];
				if (v[next.x][next.y]) continue;
				result += 1;
				q.Enqueue(next);
				v[next.x][next.y] = true;
			}
		}
		return result;
	}

	public static int CountWallsAroundCellCross(Maze maze, Vector2Int pos) {
		int result = 0;
		for (int dd = 0; dd < 4; dd++) {
			if (!IsInMaze(pos) || maze.Cells[pos.x][pos.y].Walls[dd]) result += 1;
			pos += Maze.DD[dd];
		}
		return result;
	}

	public static int CountTotalInnerWalls(Maze maze) {
		int result = 0;
		for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) {
			if (x < 5 && maze.Cells[x][y].Walls[Maze.DIRECTION_RIGHT]) result += 1;
			if (y < 5 && maze.Cells[x][y].Walls[Maze.DIRECTION_DOWN]) result += 1;
		}
		return result;
	}

	public static int TryTraverse(Maze mazeA, Maze mazeB, MonoRandom rnd, List<Vector2Int> q = null, bool[][] visitedCells = null) {
		if (q == null) q = new List<Vector2Int> { Vector2Int.zero };
		if (visitedCells == null) visitedCells = Enumerable.Range(0, Maze.SIZE.x).Select(_ => Enumerable.Repeat(false, Maze.SIZE.y).ToArray()).ToArray();
		int result = visitedCells.Sum(row => row.Count(v => v));
		while (q.Count > 0) {
			int qInd = rnd.Next(0, q.Count);
			Vector2Int pos = q[qInd];
			int lastInd = q.Count - 1;
			q[qInd] = q[lastInd];
			q.RemoveAt(lastInd);
			for (int dd = 0; dd < 4; dd++) {
				Vector2Int adj = pos + Maze.DD[dd];
				if (!IsInMaze(adj)) continue;
				if (visitedCells[adj.x][adj.y]) continue;
				if (mazeA.Cells[pos.x][pos.y].Walls[dd] && mazeB.Cells[pos.x][pos.y].Walls[dd]) continue;
				visitedCells[adj.x][adj.y] = true;
				result += 1;
				q.Add(adj);
			}
		}
		return result;
	}

	public static ulong HashMaze(Maze maze) {
		ulong result = 0;
		for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) {
			if (x < Maze.SIZE.x - 1) result = result << 1 | (maze.Cells[x][y].Walls[Maze.DIRECTION_RIGHT] ? 1ul : 0ul);
			if (y < Maze.SIZE.y - 1) result = result << 1 | (maze.Cells[x][y].Walls[Maze.DIRECTION_DOWN] ? 1ul : 0ul);
		}
		return result;
	}

	private struct WallCandidateToRemove {
		public int MazeIndex;
		public Vector2Int Position;
		public int Direction;
	}

	private struct WallCandidateToAdd {
		public int MazeIndex;
		public Vector2Int Position;
		public int Direction;
		public int RoomSize;
	}
}
