using UnityEngine;

public class MazeCellComponent : MonoBehaviour {
	public MeshFilter MeshFilter;
	public Mesh EmptyCellSharedMesh;
	public Mesh PlayerSharedMesh;

	private bool _active = false;
	public bool Active {
		get { return _active; }
		set { if (_active == value) return; _active = value; MeshFilter.sharedMesh = value ? PlayerSharedMesh : EmptyCellSharedMesh; }
	}
}
