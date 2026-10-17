using Godot;
using System.Collections.Generic;

public partial class LevelChallenge : Node3D
{
	[Export] public int BlocksToGenerate = 5; 
	
	// Pasta onde estão os seus modelos 3D das faces (Ajuste se necessário)
	[Export] public string FacesFolderPath = "res://Assets/Faces/"; 
	
	public HashSet<Vector3I> TargetTopology { get; private set; } = new HashSet<Vector3I>();
	private Dictionary<Vector3I, FaceType[]> generatedFaces = new Dictionary<Vector3I, FaceType[]>();
	
	// Armazena as Cenas 3D das faces carregadas baseadas no nome
	private Dictionary<FaceType, PackedScene> faceScenes = new Dictionary<FaceType, PackedScene>();

	// Variáveis da UI e Navegação do Minimapa
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;
	private Control uiContainer;
	private Control mapCanvas; 
	private ColorRect mapBackground;
	
	private Vector2 mapPan = Vector2.Zero;
	private float mapZoom = 0.8f;
	private bool isDraggingMap = false;

	public override void _Ready()
	{
		LoadTextures();
		LoadFaceScenes(); // Carrega os modelos 3D das faces
		
		GenerateProceduralTopology();
		BuildAndScatterPieces();
		
		CallDeferred(MethodName.CreateTargetUI);
	}

	private void LoadFaceScenes()
	{
		// Carrega diretamente os ficheiros .glb com os nomes exatos da sua pasta
		faceScenes[FaceType.Flat]     = GD.Load<PackedScene>(FacesFolderPath + "Face_Liso.glb");
		faceScenes[FaceType.QuadExt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Quad_Ext.glb");
		faceScenes[FaceType.QuadInt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Quad_Int.glb");
		faceScenes[FaceType.CircExt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Circ_Ext.glb");
		faceScenes[FaceType.CircInt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Circ_Int.glb");
		faceScenes[FaceType.CruzExt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Cruz_Ext.glb");
		faceScenes[FaceType.CruzInt]  = GD.Load<PackedScene>(FacesFolderPath + "Face_Cruz_Int.glb");
		
		// Validação de segurança no output
		foreach (var scene in faceScenes)
		{
			if (scene.Value == null) 
				GD.PrintErr($"ATENÇÃO: Não foi possível carregar o modelo 3D para {scene.Key} no caminho {FacesFolderPath}.");
		}
	}

	public override void _Process(double delta)
	{
		CheckVictoryCondition();
	}
}
