using Godot;
using System;
using System.Collections.Generic;

public partial class HeartPuzzleGenerator : Node3D
{
	// As 7 peças exportadas para o Inspector
	[Export] public PackedScene FaceLiso;
	[Export] public PackedScene FaceQuadExt;
	[Export] public PackedScene FaceQuadInt;
	[Export] public PackedScene FaceCircExt;
	[Export] public PackedScene FaceCircInt;
	[Export] public PackedScene FaceCruzExt;
	[Export] public PackedScene FaceCruzInt;
	
	private float VoxelSize = 2.0f;
	private HashSet<Vector3I> ActiveVoxels = new HashSet<Vector3I>();
	private Dictionary<string, string> processedBorders = new Dictionary<string, string>();
	private Vector3I CenterVoxel = Vector3I.Zero;

	// Matriz de rotação para as 6 direções
	// Mapeamento corrigido das 6 direções no espaço 3D do Godot
	private Vector3[] faceRotations = {
		Vector3.Zero,                                     // 0: Up (Topo)
		new Vector3((float)Math.PI, 0, 0),                // 1: Down (Baixo)
		new Vector3(0, 0, -(float)Math.PI / 2),           // 2: Right (Direita)
		new Vector3(0, 0, (float)Math.PI / 2),            // 3: Left (Esquerda)
		new Vector3(-(float)Math.PI / 2, 0, 0),           // 4: Forward (-Z) -> SINAL CORRIGIDO
		new Vector3((float)Math.PI / 2, 0, 0)             // 5: Back (+Z) -> SINAL CORRIGIDO
	};

	public override void _Ready()
	{
		GenerateHeartVolume();
		BuildPhysicsPieces();
	}

	private void GenerateHeartVolume()
	{
		int gridSize = 4;
		float scaleFactor = 0.5f;

		for (int x = -gridSize; x <= gridSize; x++)
		{
			for (int y = -gridSize; y <= gridSize; y++)
			{
				for (int z = -gridSize; z <= gridSize; z++)
				{
					float mx = x * scaleFactor; float my = y * scaleFactor; float mz = z * scaleFactor;
					float val = (mx * mx) + (2.25f * mz * mz) + (my * my) - 1.0f;
					if ((val * val * val) - (mx * mx * my * my * my) - (0.1125f * mz * mz * my * my * my) <= 0.0f)
					{
						ActiveVoxels.Add(new Vector3I(x, y, z));
					}
				}
			}
		}
		// A linha que forçava a semente central foi removida!
	}

	int spawnCounter = 0; 
	int columns = 6; // Quantidade de peças por linha na hora de cair
	float spawnSpacing = 4.0f; // Distância entre as peças no ar

	private void BuildPhysicsPieces()
	{
		Vector3I[] directions = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		foreach (Vector3I pos in ActiveVoxels)
		{
			PuzzlePiece piece = new PuzzlePiece();
			piece.TargetGridPosition = pos;
			
			CollisionShape3D collider = new CollisionShape3D();
			collider.Shape = new BoxShape3D() { Size = new Vector3(VoxelSize, VoxelSize, VoxelSize) };
			piece.AddChild(collider);

			for (int i = 0; i < 6; i++)
			{
				Vector3I neighborPos = pos + directions[i];
				FaceType assignedFace = FaceType.Flat;

				if (ActiveVoxels.Contains(neighborPos))
				{
					string borderId = GetBorderId(pos, neighborPos);
					if (!processedBorders.ContainsKey(borderId))
					{
						int shapeType = rng.RandiRange(0, 2);
						if (shapeType == 0) { assignedFace = FaceType.QuadExt; processedBorders[borderId] = "Quad"; }
						else if (shapeType == 1) { assignedFace = FaceType.CircExt; processedBorders[borderId] = "Circ"; }
						else { assignedFace = FaceType.CruzExt; processedBorders[borderId] = "Cruz"; }
					}
					else
					{
						string savedShape = processedBorders[borderId];
						if (savedShape == "Quad") assignedFace = FaceType.QuadInt;
						else if (savedShape == "Circ") assignedFace = FaceType.CircInt;
						else assignedFace = FaceType.CruzInt;
					}
				}

				piece.Faces[i] = assignedFace;
				InstantiateFaceVisuals(piece, assignedFace, i);
			}

			AddChild(piece);

			// AGORA TODAS AS PEÇAS CAEM DO CÉU ALEATORIAMENTE
			piece.Position = new Vector3(rng.RandfRange(-15, 15), rng.RandfRange(15, 30), rng.RandfRange(-15, 15));
			piece.Rotation = new Vector3(rng.RandfRange(0, 3), rng.RandfRange(0, 3), rng.RandfRange(0, 3));
		}
	}
	
// Mapeamento das 6 direções no espaço 3D do Godot
	private Vector3[] faceOffsets = {
		Vector3.Up,       // 0: Topo
		Vector3.Down,     // 1: Baixo
		Vector3.Right,    // 2: Direita
		Vector3.Left,     // 3: Esquerda
		Vector3.Forward,  // 4: Frente (-Z)
		Vector3.Back      // 5: Trás (+Z)
	};
	private void InstantiateFaceVisuals(Node3D parent, FaceType type, int dirIndex)
	{
		PackedScene scene = FaceLiso;
		
		switch (type)
		{
			case FaceType.QuadExt: scene = FaceQuadExt; break;
			case FaceType.QuadInt: scene = FaceQuadInt; break;
			case FaceType.CircExt: scene = FaceCircExt; break;
			case FaceType.CircInt: scene = FaceCircInt; break;
			case FaceType.CruzExt: scene = FaceCruzExt; break;
			case FaceType.CruzInt: scene = FaceCruzInt; break;
		}
		
		Node3D face = scene.Instantiate<Node3D>();
		
		// 1. Aplica a rotação correta
		face.Rotation = faceRotations[dirIndex];
		
		// 2. CORREÇÃO: Translada a face do centro para a borda externa.
		// A espessura da tampa no Blender era 0.2. Ao movermos (VoxelSize / 2.0f) - 0.1f, 
		// ou seja, 0.9f, garantimos que as quinas se toquem formando um cubo exato de 2x2x2.
		face.Position = faceOffsets[dirIndex] * ((VoxelSize / 2.0f) - 0.1f);
		
		parent.AddChild(face);
	}

	private string GetBorderId(Vector3I a, Vector3I b)
	{
		if (a.X < b.X || (a.X == b.X && a.Y < b.Y) || (a.X == b.X && a.Y == b.Y && a.Z < b.Z)) return $"{a}_{b}";
		return $"{b}_{a}";
	}
}
