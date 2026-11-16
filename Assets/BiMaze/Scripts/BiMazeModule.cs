using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using UnityEngine;
using wawa.Modules;
using Random = UnityEngine.Random;

public class BiMazeModule : ModdedModule {
	public const float CELLS_OFFSET = .00017f;
	public readonly string DIRECTION_TO_CHAR = "RULD";
	public readonly ReadOnlyCollection<string> DIRECTION_TO_STRING = Array.AsReadOnly(new [] { "RIGHT", "UP", "LEFT", "DOWN" });

	// self components
	public KMRuleSeedable RuleSeedable;
	public KMAudio Audio;

	public MazeCellComponent MazeCellComponentPrefab;
	public WallComponent WallSegmentPrefab;

	public GameObject ScreenOff;
	public GameObject ScreenOn;

	public Transform MazeContainer;
	public Transform IndicatorATransform;
	public Transform IndicatorBTransform;

	public KMSelectable ButtonRight;
	public KMSelectable ButtonUp;
	public KMSelectable ButtonLeft;
	public KMSelectable ButtonDown;

	public FinishComponent Finish;

	private bool _isSolved = false;
	private bool _inMazeA = true;

	private bool _isHolding = false;
	private int _holdDirection;
	private float _holdingStartTime;
	private KMSelectable _heldButton;

	private Vector2Int _playerPosition;
	private Vector2Int _finishPosition;
	private Maze _mazeA;
	private Maze _mazeB;
	private int _mazeAId;
	private int _mazeBId;
	private List<int> _path = new List<int>();
	private readonly MazeCellComponent[][] _cells = Enumerable.Range(0, Maze.SIZE.x).Select(x => Enumerable.Repeat<MazeCellComponent>(null, Maze.SIZE.y).ToArray()).ToArray();
	private readonly WallComponent[][][] _walls = Enumerable.Range(0, Maze.SIZE.x).Select(
		x => Enumerable.Range(0, Maze.SIZE.y).Select(y => Enumerable.Repeat<WallComponent>(null, 2).ToArray()).ToArray()
	).ToArray();

	protected void Start() {
		MonoRandom rnd = RuleSeedable.GetRNG();
		Log("Rule Seed: {0}", rnd.Seed);
		Maze[] bimazes = BiMazeUtils.Generate(rnd);
		int mazeAIndex = Random.Range(BiMazeUtils.BIMAZES_OFFSET, BiMazeUtils.VANILLA_MAZES_COUNT);
		int mazeBIndex = Random.Range(BiMazeUtils.BIMAZES_OFFSET, BiMazeUtils.VANILLA_MAZES_COUNT - 1);
		if (mazeBIndex >= mazeAIndex) mazeBIndex += 1;
		_mazeA = bimazes[mazeAIndex];
		_mazeB = bimazes[mazeBIndex];
		Vector2Int[] indicators = new Vector2Int[] { _mazeA.Indicators.PickRandom(), _mazeB.Indicators.PickRandom() };
		// Vector2Int[] indicators = _mazeA.Indicators.ToArray();
		{
			Vector2Int[] shuffledIndicators = indicators.ToArray();
			shuffledIndicators.Shuffle();
			IndicatorATransform.localPosition = CoordToCellPosition(shuffledIndicators[0]);
			IndicatorBTransform.localPosition = CoordToCellPosition(shuffledIndicators[1]);
		}
		int indCompare = BiMazeUtils.CompareIndicators(indicators[0], indicators[1]);
		if (indCompare > 0) {
			int temp0 = mazeAIndex;
			mazeAIndex = mazeBIndex;
			mazeBIndex = temp0;
			Maze temp1 = _mazeA;
			_mazeA = _mazeB;
			_mazeB = temp1;
			Vector2Int temp2 = indicators[0];
			indicators[0] = indicators[1];
			indicators[1] = temp2;
		}
		Log("Indicators: {0} & {1}", CoordToString(indicators[0]), CoordToString(indicators[1]));
		_mazeAId = mazeAIndex - 2;
		_mazeBId = mazeBIndex - 2;
		Log("Mazes: {0} & {1}", _mazeAId, _mazeBId);
	}

	protected override void OnActivate() {
		base.OnActivate();
		ScreenOn.SetActive(true);
		ScreenOff.SetActive(false);
		for (int x = 0; x < Maze.SIZE.x; x++) for (int y = 0; y < Maze.SIZE.y; y++) {
			MazeCellComponent cell = Instantiate(MazeCellComponentPrefab);
			cell.transform.parent = MazeContainer;
			cell.transform.localPosition = CoordToCellPosition(new Vector2Int(x, y));
			cell.transform.localScale = Vector3.one;
			cell.transform.localRotation = Quaternion.identity;
			_cells[x][y] = cell;
		}
		_playerPosition = new Vector2Int(Random.Range(0, Maze.SIZE.x), Random.Range(0, Maze.SIZE.y));
		Log("Starting location: {0}:{1}", _mazeAId, CoordToString(_playerPosition));
		_cells[_playerPosition.x][_playerPosition.y].Active = true;
		_finishPosition = BiMazeUtils.GetFinishPosition(_mazeA, _mazeB, _playerPosition);
		Log("Finish location: {0}", CoordToString(_finishPosition));
		Finish.gameObject.SetActive(true);
		Finish.transform.localPosition = CoordToCellPosition(_finishPosition);
		Finish.transform.localRotation = Quaternion.Euler(0, Random.Range(0, 360f), 0);
		_cells[_finishPosition.x][_finishPosition.y].gameObject.SetActive(false);
		ButtonRight.OnInteract += () => { OnButtonPressed(Maze.DIRECTION_RIGHT, ButtonRight); return false; };
		ButtonUp.OnInteract += () => { OnButtonPressed(Maze.DIRECTION_UP, ButtonUp); return false; };
		ButtonLeft.OnInteract += () => { OnButtonPressed(Maze.DIRECTION_LEFT, ButtonLeft); return false; };
		ButtonDown.OnInteract += () => { OnButtonPressed(Maze.DIRECTION_DOWN, ButtonDown); return false; };
		foreach (KMSelectable button in new[] { ButtonRight, ButtonUp, ButtonLeft, ButtonDown }) button.OnInteractEnded += () => { OnButtonReleased(); };
	}

	protected void Update() {
		Finish.transform.localRotation *= Quaternion.Euler(0, Time.deltaTime * 30f, 0);
		if (_isHolding && Time.time - _holdingStartTime >= 1f) {
			_path.Add((1 << 2) + _holdDirection);
			_isHolding = false;
			Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonRelease, _heldButton.transform);
			_heldButton.AddInteractionPunch(-.1f);
		}
	}

	private void OnButtonPressed(int direction, KMSelectable button) {
		Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, button.transform);
		button.AddInteractionPunch(.1f);
		if (_isSolved) return;
		Maze maze = _inMazeA ? _mazeA : _mazeB;
		if (maze.Cells[_playerPosition.x][_playerPosition.y].Walls[direction]) {
			Vector2Int next = _playerPosition + Maze.DD[direction];
			if (!BiMazeUtils.IsInMaze(next)) return;
			_path.Add(direction);
			PrintAndClearPath();
			Log("Tried to move {0} from {1}:{2}. But there's a wall. Strike!", DIRECTION_TO_STRING[direction], _inMazeA ? _mazeAId : _mazeBId, CoordToString(_playerPosition));
			Strike();
			Vector2Int wallPos = _playerPosition;
			int dd = direction;
			if (direction > 1) {
				wallPos += Maze.DD[direction];
				dd = (direction + 2) % 4;
			}
			WallComponent wall = _walls[wallPos.x][wallPos.y][dd];
			if (wall == null) {
				wall = Instantiate(WallSegmentPrefab);
				wall.transform.parent = MazeContainer;
				wall.transform.localPosition = CoordToCellPosition(_playerPosition);
				wall.transform.localScale = Vector3.one;
				wall.transform.localRotation = Quaternion.Euler(0, -90f * direction, 0);
				_walls[wallPos.x][wallPos.y][dd] = wall;
			}
			if (_inMazeA) wall.MazeA = true;
			else wall.MazeB = true;
		} else {
			_cells[_playerPosition.x][_playerPosition.y].Active = false;
			_playerPosition += Maze.DD[direction];
			_cells[_playerPosition.x][_playerPosition.y].Active = true;
			if (_playerPosition.x == _finishPosition.x && _playerPosition.y == _finishPosition.y) {
				_path.Add(direction);
				PrintAndClearPath();
				Log("Solved");
				Solve();
				_isSolved = true;
				Audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.CorrectChime, transform);
			} else {
				_isHolding = true;
				_holdDirection = direction;
				_holdingStartTime = Time.time;
				_heldButton = button;
			}
		}
	}

	private void OnButtonReleased() {
		if (_isSolved) return;
		if (!_isHolding) return;
		_path.Add(_holdDirection);
		_isHolding = false;
		_inMazeA = !_inMazeA;
		Finish.InMazeA = _inMazeA;
	}

	private Vector3 CoordToCellPosition(Vector2Int coord) {
		Vector2 pos = (Vector2)coord - (Maze.SIZE - Vector2.one) / 2f;
		pos = new Vector2(pos.x * CELLS_OFFSET, pos.y * CELLS_OFFSET);
		// vanilla uses (0,-1) as up and (0,+1) as down
		return new Vector3(pos.x, 0, -pos.y);
	}

	private string CoordToString(Vector2Int pos) {
		return (char)('A' + pos.x) + (pos.y + 1).ToString();
	}

	private void PrintAndClearPath() {
		Log("Path: {0}", _path.Select(d => ((d >> 2) > 0 ? "h" : "") + DIRECTION_TO_CHAR[d & 3]).Join(" "));
		_path.Clear();
	}
}
