using Godot;
using System;

// O modificador 'partial' permite que a classe seja dividida.
public partial class OrbitCamera : Node3D
{
	[Export] public float Sensitivity = 0.005f;
	[Export] public float MoveSpeed = 15.0f;
	[Export] public float ZoomSpeed = 3.0f;
	[Export] public float MinZoom = 2.0f;     
	[Export] public float MaxZoom = 200.0f;   

	private bool isOrbiting = false;
	private Camera3D camera;
	
	private PuzzlePiece selectedPiece = null;
	private Node3D coreGroup;       
	private bool isHoldingCore = false; 

	// Fantasma de Encaixe
	private MeshInstance3D snapGhost;
	private StandardMaterial3D ghostMat;

	private bool isCurrentlyValidSnap = false;
	private Vector3 currentSnapWorldPos;
	private Vector3 targetPiecePosition;
	private Vector3 targetCameraPosition;

	// UI e Minimapa 3D
	private Label helpLabel;
	private SubViewport minimapViewport;
	private Camera3D minimapCamera;
	private Node3D minimapRoot;

	public override void _Ready()
	{
		camera = GetNode<Camera3D>("Camera3D");
		targetCameraPosition = GlobalPosition; 
		
		SetupGhostCube(); // Definido em OrbitCamera.Logic.cs

		coreGroup = new Node3D();
		coreGroup.Name = "CoreAssembly";
		GetParent().CallDeferred(Node.MethodName.AddChild, coreGroup);

		Build3DMinimapUI(); // Definido em OrbitCamera.UI.cs
	}

	public override void _Process(double delta)
	{
		UpdateUIProcess(); // Definido em OrbitCamera.UI.cs
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovementProcess(delta); // Definido em OrbitCamera.Logic.cs
		HandleSnappingProcess(delta); // Definido em OrbitCamera.Logic.cs
	}
}
