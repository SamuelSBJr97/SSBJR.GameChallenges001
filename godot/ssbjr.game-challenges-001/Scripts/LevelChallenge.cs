using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class LevelChallenge : Node3D
{
	[Export] public int BlocksToGenerate = 5; // Dificuldade do nível
	
	// Assinatura topológica alvo para validar a vitória
	public HashSet<Vector3I> TargetTopology { get; private set; } = new HashSet<Vector3I>();
	
	// Dicionário de junções matemáticas geradas proceduralmente
	private Dictionary<Vector3I, FaceType[]> generatedFaces = new Dictionary<Vector3I, FaceType[]>();

	public override void _Ready()
	{
		GenerateProceduralTopology();
		BuildAndScatterPieces();
		
		// Renderiza o alvo na UI (Esquerda)
		CallDeferred(MethodName.CreateTargetMinimap);
	}

	private void GenerateProceduralTopology()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		TargetTopology.Add(Vector3I.Zero);

		Vector3I[] dirs = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };

		// Crescimento Fractal: Adiciona blocos adjacentes até atingir o número pretendido
		while (TargetTopology.Count < BlocksToGenerate)
		{
			Vector3I randomExisting = TargetTopology.ElementAt(rng.RandiRange(0, TargetTopology.Count - 1));
			Vector3I randomDir = dirs[rng.RandiRange(0, 5)];
			Vector3I newHole = randomExisting + randomDir;

			if (!TargetTopology.Contains(newHole))
			{
				TargetTopology.Add(newHole);
			}
		}

		// Prepara arrays de faces (Flat por defeito)
		foreach (Vector3I pos in TargetTopology)
		{
			generatedFaces[pos] = new FaceType[6] { FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat };
		}

		// Mapeia e emparelha os conectores para que o puzzle tenha uma solução perfeita matemática
		List<Vector3I> list = TargetTopology.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				Vector3I diff = list[j] - list[i];
				int dist = Mathf.Abs(diff.X) + Mathf.Abs(diff.Y) + Mathf.Abs(diff.Z);

				if (dist == 1) // São vizinhos
				{
					// Define o emparelhamento lógico aleatório
					int shapeType = rng.RandiRange(0, 2);
					FaceType faceA = FaceType.Flat;
					FaceType faceB = FaceType.Flat;

					if (shapeType == 0) { faceA = FaceType.QuadExt; faceB = FaceType.QuadInt; }
					else if (shapeType == 1) { faceA = FaceType.CircExt; faceB = FaceType.CircInt; }
					else { faceA = FaceType.CruzExt; faceB = FaceType.CruzInt; }

					// Se A for o cubo à direita de B
					if (diff == Vector3I.Right) { generatedFaces[list[i]][2] = faceA; generatedFaces[list[j]][3] = faceB; }
					else if (diff == Vector3I.Left) { generatedFaces[list[i]][3] = faceA; generatedFaces[list[j]][2] = faceB; }
					else if (diff == Vector3I.Up) { generatedFaces[list[i]][0] = faceA; generatedFaces[list[j]][1] = faceB; }
					else if (diff == Vector3I.Down) { generatedFaces[list[i]][1] = faceA; generatedFaces[list[j]][0] = faceB; }
					else if (diff == Vector3I.Back) { generatedFaces[list[i]][5] = faceA; generatedFaces[list[j]][4] = faceB; }
					else if (diff == Vector3I.Forward) { generatedFaces[list[i]][4] = faceA; generatedFaces[list[j]][5] = faceB; }
				}
			}
		}
	}

	private void BuildAndScatterPieces()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		foreach (var kvp in generatedFaces)
		{
			Vector3I originalGridPos = kvp.Key;
			FaceType[] faces = kvp.Value;

			// Instancia a peça dinamicamente
			PuzzlePiece piece = new PuzzlePiece();
			piece.TargetGridPosition = originalGridPos;
			
			// Adiciona colisão
			CollisionShape3D collider = new CollisionShape3D();
			collider.Shape = new BoxShape3D() { Size = new Vector3(2f, 2f, 2f) };
			piece.AddChild(collider);

			// Transfere as faces geradas matematicamente para o objeto
			for (int i = 0; i < 6; i++)
			{
				piece.Faces[i] = faces[i];
				InstantiateFaceVisuals(piece, faces[i], i);
			}

			AddChild(piece);

			// Espalha a peça aleatoriamente no céu
			piece.GlobalPosition = new Vector3(rng.RandfRange(-15, 15), rng.RandfRange(15, 30), rng.RandfRange(-15, 15));
			piece.Rotation = new Vector3(rng.RandfRange(0, 3), rng.RandfRange(0, 3), rng.RandfRange(0, 3));
		}
	}

	// Método auxiliar (Pode copiar a lógica do seu antigo HeartPuzzleGenerator)
	private void InstantiateFaceVisuals(PuzzlePiece piece, FaceType assignedFace, int faceIndex)
	{
		// ... (Insira aqui o código que gera as Meshes/Cores baseadas no faceIndex e assignedFace) ...
	}

	// === UI E VALIDAÇÃO ===
	private void CreateTargetMinimap()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		Label label = new Label();
		label.Text = "OBJETIVO DO NÍVEL";
		label.Position = new Vector2(20, 20);
		label.LabelSettings = new LabelSettings() { FontSize = 18, FontColor = Colors.Yellow };
		canvas.AddChild(label);

		SubViewportContainer container = new SubViewportContainer();
		container.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		container.Position = new Vector2(20, 50);
		container.Size = new Vector2(250, 250);
		canvas.AddChild(container);

		ColorRect bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.4f);
		bg.Size = new Vector2(250, 250);
		container.AddChild(bg);

		SubViewport viewport = new SubViewport();
		viewport.TransparentBg = true;
		viewport.Size = new Vector2I(250, 250);
		viewport.OwnWorld3D = true;
		container.AddChild(viewport);

		Node3D targetRoot = new Node3D();
		viewport.AddChild(targetRoot);

		Camera3D cam = new Camera3D();
		cam.Position = new Vector3(10, 10, 10);
		cam.LookAt(Vector3.Zero);
		viewport.AddChild(cam);

		DirectionalLight3D light = new DirectionalLight3D();
		light.Position = new Vector3(5, 5, 5);
		light.LookAt(Vector3.Zero);
		viewport.AddChild(light);

		// Constrói a topologia alvo invisível apenas para renderizar na UI
		foreach (var kvp in generatedFaces)
		{
			MeshInstance3D dummyBlock = new MeshInstance3D();
			BoxMesh box = new BoxMesh() { Size = new Vector3(2f, 2f, 2f) };
			dummyBlock.Mesh = box;
			dummyBlock.Position = new Vector3(kvp.Key.X * 2f, kvp.Key.Y * 2f, kvp.Key.Z * 2f);
			
			StandardMaterial3D mat = new StandardMaterial3D();
			mat.AlbedoColor = Colors.Cyan;
			mat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
			mat.AlbedoColor = new Color(0, 1, 1, 0.7f); // Holograma azul
			dummyBlock.MaterialOverride = mat;

			targetRoot.AddChild(dummyBlock);
		}

		// Roda lentamente o alvo para o jogador perceber o formato 3D
		Tween tween = GetTree().CreateTween().SetLoops();
		tween.TweenProperty(targetRoot, "rotation", new Vector3(0, Mathf.Pi * 2, 0), 10.0f).AsRelative();
	}

	public override void _Process(double delta)
	{
		CheckVictoryCondition();
	}

	private void CheckVictoryCondition()
	{
		// Procura o núcleo que o jogador está a construir
		Node3D core = GetNodeOrNull<Node3D>("../CoreAssembly");
		if (core == null || core.GetChildCount() != BlocksToGenerate) return;

		HashSet<Vector3I> playerTopology = new HashSet<Vector3I>();
		foreach (Node n in core.GetChildren())
		{
			if (n is PuzzlePiece p)
			{
				playerTopology.Add(new Vector3I(
					Mathf.RoundToInt(p.GlobalPosition.X / 2f),
					Mathf.RoundToInt(p.GlobalPosition.Y / 2f),
					Mathf.RoundToInt(p.GlobalPosition.Z / 2f)
				));
			}
		}

		// Normaliza as duas topologias para (0,0,0) para comparar a assinatura independente da posição/rotação
		if (AreTopologiesMatch(TargetTopology, playerTopology))
		{
			GD.Print("VITÓRIA! NÍVEL CONCLUÍDO!");
			SetProcess(false); // Pára de validar
		}
	}

	private bool AreTopologiesMatch(HashSet<Vector3I> target, HashSet<Vector3I> player)
	{
		// 1. Encontra o bounding box inferior de ambos
		Vector3I minTarget = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
		foreach (var v in target)
		{
			if (v.X < minTarget.X) minTarget.X = v.X;
			if (v.Y < minTarget.Y) minTarget.Y = v.Y;
			if (v.Z < minTarget.Z) minTarget.Z = v.Z;
		}

		Vector3I minPlayer = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
		foreach (var v in player)
		{
			if (v.X < minPlayer.X) minPlayer.X = v.X;
			if (v.Y < minPlayer.Y) minPlayer.Y = v.Y;
			if (v.Z < minPlayer.Z) minPlayer.Z = v.Z;
		}

		// 2. Compara os conjuntos normalizados
		HashSet<Vector3I> normTarget = new HashSet<Vector3I>(target.Select(v => v - minTarget));
		HashSet<Vector3I> normPlayer = new HashSet<Vector3I>(player.Select(v => v - minPlayer));

		return normTarget.SetEquals(normPlayer);
	}
}
