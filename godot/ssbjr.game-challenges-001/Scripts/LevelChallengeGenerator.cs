using Godot;
using System;
using System.Collections.Generic;
using System.Linq;

public partial class LevelChallenge
{
	// A matriz de rotações corrigidas para aplicar nas faces instanciadas
	private Vector3[] faceRotations = {
		Vector3.Zero,                                     // 0: Up (Topo)
		new Vector3((float)Math.PI, 0, 0),                // 1: Down (Baixo)
		new Vector3(0, 0, -(float)Math.PI / 2),           // 2: Right (Direita)
		new Vector3(0, 0, (float)Math.PI / 2),            // 3: Left (Esquerda)
		new Vector3(-(float)Math.PI / 2, 0, 0),           // 4: Forward (-Z)
		new Vector3((float)Math.PI / 2, 0, 0)             // 5: Back (+Z)
	};

	private void GenerateProceduralTopology()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		TargetTopology.Clear();
		generatedFaces.Clear();

		TargetTopology.Add(Vector3I.Zero);
		Vector3I[] dirs = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };

		// GRAFO COMPACTO
		while (TargetTopology.Count < BlocksToGenerate)
		{
			Dictionary<Vector3I, int> holeScores = new Dictionary<Vector3I, int>();
			foreach (Vector3I pos in TargetTopology)
			{
				foreach (Vector3I dir in dirs)
				{
					Vector3I hole = pos + dir;
					if (!TargetTopology.Contains(hole))
					{
						if (holeScores.ContainsKey(hole)) holeScores[hole]++;
						else holeScores[hole] = 1;
					}
				}
			}

			int maxScore = holeScores.Values.Max();
			var bestHoles = holeScores.Where(k => k.Value == maxScore || rng.Randf() > 0.8f).Select(k => k.Key).ToList();
			Vector3I chosenHole = bestHoles[rng.RandiRange(0, bestHoles.Count - 1)];

			TargetTopology.Add(chosenHole);
		}

		foreach (Vector3I pos in TargetTopology)
			generatedFaces[pos] = new FaceType[6] { FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat, FaceType.Flat };

		// CONSTRUÇÃO MATEMÁTICA (Chaves e Fechaduras)
		List<Vector3I> list = TargetTopology.ToList();
		for (int i = 0; i < list.Count; i++)
		{
			for (int j = i + 1; j < list.Count; j++)
			{
				Vector3I diff = list[j] - list[i];
				int dist = Mathf.Abs(diff.X) + Mathf.Abs(diff.Y) + Mathf.Abs(diff.Z);

				if (dist == 1) 
				{
					int shapeType = rng.RandiRange(0, 2);
					FaceType faceA = FaceType.Flat, faceB = FaceType.Flat;

					if (shapeType == 0) { faceA = FaceType.QuadExt; faceB = FaceType.QuadInt; }
					else if (shapeType == 1) { faceA = FaceType.CircExt; faceB = FaceType.CircInt; }
					else { faceA = FaceType.CruzExt; faceB = FaceType.CruzInt; }

					bool swap = rng.RandiRange(0, 1) == 1;
					FaceType finalA = swap ? faceA : faceB;
					FaceType finalB = swap ? faceB : faceA;

					if (diff == Vector3I.Right)      { generatedFaces[list[i]][2] = finalA; generatedFaces[list[j]][3] = finalB; }
					else if (diff == Vector3I.Left)  { generatedFaces[list[i]][3] = finalA; generatedFaces[list[j]][2] = finalB; }
					else if (diff == Vector3I.Up)    { generatedFaces[list[i]][0] = finalA; generatedFaces[list[j]][1] = finalB; }
					else if (diff == Vector3I.Down)  { generatedFaces[list[i]][1] = finalA; generatedFaces[list[j]][0] = finalB; }
					else if (diff == Vector3I.Back)  { generatedFaces[list[i]][5] = finalA; generatedFaces[list[j]][4] = finalB; }
					else if (diff == Vector3I.Forward){generatedFaces[list[i]][4] = finalA; generatedFaces[list[j]][5] = finalB; }
				}
			}
		}

		// COMPORTAMENTO EXTERIOR ALEATÓRIO
		foreach (Vector3I pos in TargetTopology)
		{
			for (int i = 0; i < 6; i++)
			{
				if (generatedFaces[pos][i] == FaceType.Flat)
				{
					if (rng.Randf() > 0.4f) 
					{
						int shapeType = rng.RandiRange(0, 2);
						bool isExt = rng.RandiRange(0, 1) == 1;
						FaceType f = FaceType.Flat;

						if (shapeType == 0) f = isExt ? FaceType.QuadExt : FaceType.QuadInt;
						else if (shapeType == 1) f = isExt ? FaceType.CircExt : FaceType.CircInt;
						else f = isExt ? FaceType.CruzExt : FaceType.CruzInt;

						generatedFaces[pos][i] = f;
					}
				}
			}
		}
	}

	private void BuildAndScatterPieces()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		// Vetores de translação exatos para afastar a face do centro e colá-la na borda do cubo (Tamanho 2x2x2)
		// O raio a partir do centro (0,0,0) para uma face de um cubo de tamanho 2 é 1.0 unidade.
		Vector3[] faceTranslations = {
			new Vector3(0, 1.0f, 0),   // 0: Up (Topo)
			new Vector3(0, -1.0f, 0),  // 1: Down (Baixo)
			new Vector3(1.0f, 0, 0),   // 2: Right (Direita)
			new Vector3(-1.0f, 0, 0),  // 3: Left (Esquerda)
			new Vector3(0, 0, -1.0f),  // 4: Forward (-Z)
			new Vector3(0, 0, 1.0f)    // 5: Back (+Z)
		};

		foreach (var kvp in generatedFaces)
		{
			PuzzlePiece piece = new PuzzlePiece();
			piece.TargetGridPosition = kvp.Key;
			
			piece.Freeze = false;
			piece.Mass = 1.0f;

			// Hitbox física principal do bloco
			CollisionShape3D collider = new CollisionShape3D();
			collider.Shape = new BoxShape3D() { Size = new Vector3(2f, 2f, 2f) };
			piece.AddChild(collider);

			// MONTAGEM DO CUBO 3D: Posiciona cada face na sua respetiva parede exterior
			for (int i = 0; i < 6; i++) 
			{
				FaceType currentType = kvp.Value[i];
				piece.Faces[i] = currentType; 

				if (faceScenes.ContainsKey(currentType) && faceScenes[currentType] != null)
				{
					Node3D faceMesh = faceScenes[currentType].Instantiate<Node3D>();
					
					// Aplica a translação para afastar a face do centro para a parede do cubo
					faceMesh.Position = faceTranslations[i];
					
					// Aplica a rotação matemática para orientar a face corretamente para fora
					faceMesh.Rotation = faceRotations[i];
					
					piece.AddChild(faceMesh);
				}
			}

			AddChild(piece);

			// Espalha pelo mapa no ar
			piece.GlobalPosition = new Vector3(rng.RandfRange(-15, 15), rng.RandfRange(15, 30), rng.RandfRange(-15, 15));
			
			// Rotação aleatória travada a eixos de 90 graus
			float angle = Mathf.Pi / 2f;
			piece.Rotation = new Vector3(rng.RandiRange(0, 3) * angle, rng.RandiRange(0, 3) * angle, rng.RandiRange(0, 3) * angle);
		}
	}

	private void CheckVictoryCondition()
	{
		Node3D core = GetNodeOrNull<Node3D>("../CoreAssembly");
		if (core == null || core.GetChildCount() != BlocksToGenerate) return;

		HashSet<Vector3I> playerTopology = new HashSet<Vector3I>();
		foreach (Node n in core.GetChildren())
		{
			if (n is PuzzlePiece p)
				playerTopology.Add(new Vector3I(Mathf.RoundToInt(p.GlobalPosition.X / 2f), Mathf.RoundToInt(p.GlobalPosition.Y / 2f), Mathf.RoundToInt(p.GlobalPosition.Z / 2f)));
		}

		if (AreTopologiesMatch(TargetTopology, playerTopology))
		{
			GD.Print("VITÓRIA! NÍVEL CONCLUÍDO!");
			SetProcess(false); 
		}
	}

	private bool AreTopologiesMatch(HashSet<Vector3I> target, HashSet<Vector3I> player)
	{
		Vector3I minTarget = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
		foreach (var v in target) { if (v.X < minTarget.X) minTarget.X = v.X; if (v.Y < minTarget.Y) minTarget.Y = v.Y; if (v.Z < minTarget.Z) minTarget.Z = v.Z; }
		
		Vector3I minPlayer = new Vector3I(int.MaxValue, int.MaxValue, int.MaxValue);
		foreach (var v in player) { if (v.X < minPlayer.X) minPlayer.X = v.X; if (v.Y < minPlayer.Y) minPlayer.Y = v.Y; if (v.Z < minPlayer.Z) minPlayer.Z = v.Z; }

		HashSet<Vector3I> normTarget = new HashSet<Vector3I>(target.Select(v => v - minTarget));
		HashSet<Vector3I> normPlayer = new HashSet<Vector3I>(player.Select(v => v - minPlayer));

		return normTarget.SetEquals(normPlayer);
	}
}
