using UnityEngine;

public class WallComponent : MonoBehaviour {
	public Renderer Renderer;

	private bool _mazeA = false;
	public bool MazeA { get { return _mazeA; } set { _mazeA = value; UpdateRenderer(); } }

	private bool _mazeB = false;
	public bool MazeB { get { return _mazeB; } set { _mazeB = value; UpdateRenderer(); } }

	private void UpdateRenderer() {
		gameObject.SetActive(_mazeA || _mazeB);
		Renderer.material.color = _mazeA && _mazeB ? Color.yellow : _mazeA ? Color.red : Color.green;
	}
}
