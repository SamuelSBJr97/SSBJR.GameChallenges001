using Godot;
using System.Collections.Generic;

public partial class LevelChallenge : Node3D
{
	[Export] public int BlocksToGenerate = 5; 
	
	// OBRIGATÓRIO: Arraste a sua cena "PuzzlePiece.tscn" para aqui no Inspector do Godot!
	[Export] public PackedScene PieceScene; 
	
	public HashSet<Vector3I> TargetTopology { get; private set; } = new HashSet<Vector3I>();
	private Dictionary<Vector3I, FaceType[]> generatedFaces = new Dictionary<Vector3I, FaceType[]>();

	// Variáveis da UI e Navegação do Minimapa
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;
	private Control uiContainer;
	private Control mapCanvas; 
	private ColorRect mapBackground;
	
	// Controle de Zoom e Pan
	private Vector2 mapPan = Vector2.Zero;
	private float mapZoom = 0.8f;
	private bool isDraggingMap = false;

	public override void _Ready()
	{
		LoadTextures();
		GenerateProceduralTopology();
		BuildAndScatterPieces();
		
		CallDeferred(MethodName.CreateTargetUI);
	}

	public override void _Process(double delta)
	{
		CheckVictoryCondition();
	}
}
