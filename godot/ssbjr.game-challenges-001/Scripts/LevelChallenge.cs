using Godot;
using System.Collections.Generic;

public partial class LevelChallenge : Node3D
{
	[Export] public int BlocksToGenerate = 5; 
	
	public HashSet<Vector3I> TargetTopology { get; private set; } = new HashSet<Vector3I>();
	private Dictionary<Vector3I, FaceType[]> generatedFaces = new Dictionary<Vector3I, FaceType[]>();

	// Variáveis da UI e Navegação do Minimapa
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;
	private Control uiContainer;
	private Control mapCanvas; // Onde os ícones são desenhados (Permite Zoom/Pan)
	private ColorRect mapBackground;
	
	// Controle de Zoom e Pan
	private Vector2 mapPan = Vector2.Zero;
	private float mapZoom = 1.0f;
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
