using UnityEngine;

public class FinishComponent : MonoBehaviour {
	public Renderer Renderer;

	private bool _inMazeA = true;
	public bool InMazeA { get { return _inMazeA; } set { _inMazeA = value; UpdateTriangleColor(); } }

	protected void Start() {
		UpdateTriangleColor();
	}

	public void UpdateTriangleColor() {
		Renderer.material.color = _inMazeA ? Color.red : Color.green;
	}
}
