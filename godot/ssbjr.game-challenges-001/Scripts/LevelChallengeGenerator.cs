using Godot;
using System.Collections.Generic;
using System.Linq;

public partial class LevelChallenge
{
	private void GenerateProceduralTopology()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		TargetTopology.Clear();
		generatedFaces.Clear();

		// 1. Inicia o primeiro bloco no centro
		TargetTopology.Add(Vector3I.Zero);
		generatedFaces[Vector3I.Zero] = new FaceType[6]; // Começa tudo Flat

		Vector3I[] dirs = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };
		List<Vector3I> existingNodes = new List<Vector3I> { Vector3I.Zero };

		// 2. Crescimento em Árvore (Garante solução única e 100% de encaixe)
		while (TargetTopology.Count < BlocksToGenerate)
		{
			Vector3I randomExisting = existingNodes[rng.RandiRange(0, existingNodes.Count - 1)];
			Vector3I randomDir = dirs[rng.RandiRange(0, 5)];
			Vector3I newHole = randomExisting + randomDir;

			if (!TargetTopology.Contains(newHole))
			{
				TargetTopology.Add(newHole);
				existingNodes.Add(newHole);
				generatedFaces[newHole] = new FaceType[6]; // Inicializa a zero (Flat)

				// 3. Cria a fechadura (Macho/Fêmea) EXCLUSIVAMENTE nesta ligação!
				int shapeType = rng.RandiRange(0, 2);
				FaceType faceExt = FaceType.Flat, faceInt = FaceType.Flat;

				if (shapeType == 0) { faceExt = FaceType.QuadExt; faceInt = FaceType.QuadInt; }
				else if (shapeType == 1) { faceExt = FaceType.CircExt; faceInt = FaceType.CircInt; }
				else { faceExt = FaceType.CruzExt; faceInt = FaceType.CruzInt; }

				// Decide aleatoriamente qual bloco fica com o Macho e qual fica com a Fêmea
				bool swap = rng.RandiRange(0, 1) == 1;
				FaceType faceA = swap ? faceExt : faceInt;
				FaceType faceB = swap ? faceInt : faceExt;

				// Atribui as faces exatamente no eixo de contacto
				if (randomDir == Vector3I.Up)        { generatedFaces[randomExisting][0] = faceA; generatedFaces[newHole][1] = faceB; }
				else if (randomDir == Vector3I.Down) { generatedFaces[randomExisting][1] = faceA; generatedFaces[newHole][0] = faceB; }
				else if (randomDir == Vector3I.Right){ generatedFaces[randomExisting][2] = faceA; generatedFaces[newHole][3] = faceB; }
				else if (randomDir == Vector3I.Left) { generatedFaces[randomExisting][3] = faceA; generatedFaces[newHole][2] = faceB; }
				else if (randomDir == Vector3I.Forward){ generatedFaces[randomExisting][4] = faceA; generatedFaces[newHole][5] = faceB; }
				else if (randomDir == Vector3I.Back) { generatedFaces[randomExisting][5] = faceA; generatedFaces[newHole][4] = faceB; }
			}
		}
	}

	private void BuildAndScatterPieces()
	{
		RandomNumberGenerator rng = new RandomNumberGenerator();
		rng.Randomize();

		foreach (var kvp in generatedFaces)
		{
			PuzzlePiece piece = new PuzzlePiece();
			piece.TargetGridPosition = kvp.Key;
			
			piece.Freeze = false;
			piece.Mass = 1.0f;

			CollisionShape3D collider = new CollisionShape3D();
			collider.Shape = new BoxShape3D() { Size = new Vector3(2f, 2f, 2f) };
			piece.AddChild(collider);

			// Transfere as faces precisas para o bloco
			for (int i = 0; i < 6; i++) piece.Faces[i] = kvp.Value[i];

			AddChild(piece);
			piece.GlobalPosition = new Vector3(rng.RandfRange(-10, 10), rng.RandfRange(15, 30), rng.RandfRange(-10, 10));
			
			// Dá uma rotação maluca (multiplos de 90 graus) para o jogador ter de descobrir
			float angle = Mathf.Pi / 2f;
			piece.Rotation = new Vector3(
				rng.RandiRange(0, 3) * angle, 
				rng.RandiRange(0, 3) * angle, 
				rng.RandiRange(0, 3) * angle
			);
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
			{
				playerTopology.Add(new Vector3I(
					Mathf.RoundToInt(p.GlobalPosition.X / 2f),
					Mathf.RoundToInt(p.GlobalPosition.Y / 2f),
					Mathf.RoundToInt(p.GlobalPosition.Z / 2f)
				));
			}
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
