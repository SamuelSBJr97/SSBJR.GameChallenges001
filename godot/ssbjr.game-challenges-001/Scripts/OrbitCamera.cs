using Godot;
using System;
using System.Collections.Generic;

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

	private MeshInstance3D snapGhost;
	private StandardMaterial3D ghostMat;

	private bool isCurrentlyValidSnap = false;
	private Vector3 currentSnapWorldPos;
	private Vector3 targetPiecePosition;
	private Vector3 targetCameraPosition;

	private List<PuzzlePiece> snapTargetPieces = new List<PuzzlePiece>();

	public override void _Ready()
	{
		camera = GetNode<Camera3D>("Camera3D");
		targetCameraPosition = GlobalPosition; 
		
		SetupGhostCube(); 

		coreGroup = new Node3D();
		coreGroup.Name = "CoreAssembly";
		GetParent().CallDeferred(Node.MethodName.AddChild, coreGroup);

		LoadTextures();
		Build2DMinimapUI(); 
	}

	public override void _Process(double delta)
	{
		UpdateUIProcess(); 
	}

	public override void _PhysicsProcess(double delta)
	{
		HandleMovementProcess(delta); 
		HandleSnappingProcess(delta); 
	}
}
