using Godot;
using System;
using System.Collections.Generic;

public partial class OrbitCamera
{
	private void SetupGhostCube()
	{
		snapGhost = new MeshInstance3D();
		BoxMesh box = new BoxMesh();
		box.Size = new Vector3(2.1f, 2.1f, 2.1f);
		snapGhost.Mesh = box;

		ghostMat = new StandardMaterial3D();
		ghostMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		ghostMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		ghostMat.NoDepthTest = true; 
		snapGhost.MaterialOverride = ghostMat;
		snapGhost.TopLevel = true;
		snapGhost.Visible = false;
		AddChild(snapGhost);
	}

	private void HandleMovementProcess(double delta)
	{
		if (selectedPiece == null && !isHoldingCore) return;

		Vector3 moveInput = Vector3.Zero;
		if (Input.IsActionPressed("move_right")) moveInput.X += 1;
		if (Input.IsActionPressed("move_left"))  moveInput.X -= 1;
		if (Input.IsActionPressed("move_back"))  moveInput.Z += 1;
		if (Input.IsActionPressed("move_forward")) moveInput.Z -= 1;
		if (Input.IsActionPressed("move_up"))    moveInput.Y += 1;
		if (Input.IsActionPressed("move_down"))  moveInput.Y -= 1;

		if (moveInput.LengthSquared() > 0) moveInput = moveInput.Normalized();

		if (isOrbiting)
		{
			Vector3 rawForward = -camera.GlobalTransform.Basis.Z.Normalized();
			Vector3 rawRight = camera.GlobalTransform.Basis.X.Normalized();
			Vector3 freeMove = (rawRight * moveInput.X) + (Vector3.Up * moveInput.Y) + (rawForward * moveInput.Z);

			targetCameraPosition += freeMove * MoveSpeed * 4.0f * (float)delta;
			targetCameraPosition.X = Mathf.Clamp(targetCameraPosition.X, -150f, 150f);
			targetCameraPosition.Y = Mathf.Clamp(targetCameraPosition.Y, -30f, 150f); 
			targetCameraPosition.Z = Mathf.Clamp(targetCameraPosition.Z, -150f, 150f);

			GlobalPosition = GlobalPosition.Lerp(targetCameraPosition, (float)delta * 15f);
		}
		else
		{
			Vector3 camRight = camera.GlobalTransform.Basis.X.Normalized();
			Vector3 camForward = -camera.GlobalTransform.Basis.Z.Normalized();
			camRight.Y = 0; camRight = camRight.Normalized();
			camForward.Y = 0; camForward = camForward.Normalized();

			Vector3 worldMove = (camRight * moveInput.X) + (Vector3.Up * moveInput.Y) + (camForward * moveInput.Z);

			Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;
			if (activeNode != null)
			{
				targetPiecePosition += worldMove * MoveSpeed * (float)delta;
				activeNode.GlobalPosition = activeNode.GlobalPosition.Lerp(targetPiecePosition, (float)delta * 12f);
			}
		}
	}

	private void HandleSnappingProcess(double delta)
	{
		if (selectedPiece != null && !isHoldingCore)
		{
			if (coreGroup.GetChildCount() == 0)
			{
				Vector3 pos = selectedPiece.GlobalPosition;
				Vector3I gridPos = new Vector3I(Mathf.RoundToInt(pos.X / 2f), Mathf.RoundToInt(pos.Y / 2f), Mathf.RoundToInt(pos.Z / 2f));
				currentSnapWorldPos = new Vector3(gridPos.X * 2f, gridPos.Y * 2f, gridPos.Z * 2f);
				
				snapGhost.GlobalPosition = currentSnapWorldPos;
				snapGhost.Visible = true;
				ghostMat.AlbedoColor = new Color(0, 1, 0, 0.4f);
				isCurrentlyValidSnap = true;
			}
			else
			{
				Vector3 piecePos = selectedPiece.GlobalPosition;
				
				HashSet<Vector3I> occupiedSlots = new HashSet<Vector3I>();
				foreach (Node node in coreGroup.GetChildren())
				{
					if (node is PuzzlePiece p)
					{
						occupiedSlots.Add(new Vector3I(
							Mathf.RoundToInt(p.GlobalPosition.X / 2f), 
							Mathf.RoundToInt(p.GlobalPosition.Y / 2f), 
							Mathf.RoundToInt(p.GlobalPosition.Z / 2f)
						));
					}
				}

				Vector3I[] dirs = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };
				HashSet<Vector3I> validHoles = new HashSet<Vector3I>();
				
				foreach (Vector3I occ in occupiedSlots)
				{
					foreach (Vector3I d in dirs)
					{
						Vector3I hole = occ + d;
						if (!occupiedSlots.Contains(hole)) validHoles.Add(hole);
					}
				}

				float minDistance = 4.0f; 
				bool foundSnap = false;
				Vector3I bestHole = Vector3I.Zero;

				foreach (Vector3I hole in validHoles)
				{
					Vector3 holeWorldPos = new Vector3(hole.X * 2f, hole.Y * 2f, hole.Z * 2f);
					float dist = piecePos.DistanceTo(holeWorldPos);
					if (dist < minDistance)
					{
						minDistance = dist;
						bestHole = hole;
						foundSnap = true;
					}
				}

				if (foundSnap)
				{
					currentSnapWorldPos = new Vector3(bestHole.X * 2f, bestHole.Y * 2f, bestHole.Z * 2f);
					
					bool isValid = ValidateSpecificSnapPosition(bestHole, occupiedSlots);
					isCurrentlyValidSnap = isValid;

					snapGhost.GlobalPosition = currentSnapWorldPos;
					snapGhost.Visible = true;

					// Apenas atualiza a cor do feedback visual. Aguarda o input manual para o drop real.
					if (isValid)
					{
						ghostMat.AlbedoColor = new Color(0, 1, 0, 0.4f); // Verde
					}
					else
					{
						ghostMat.AlbedoColor = new Color(1, 0, 0, 0.5f); // Vermelho
					}
				}
				else
				{
					snapGhost.Visible = false;
					isCurrentlyValidSnap = false;
				}
			}
		}
		else if (isHoldingCore)
		{
			snapGhost.Visible = false;
		}
	}

	private void SnapPieceRotationToGrid(Node3D node)
	{
		Vector3 euler = node.GlobalRotation;
		float step = Mathf.Pi / 2f;
		node.GlobalRotation = new Vector3(
			Mathf.Round(euler.X / step) * step,
			Mathf.Round(euler.Y / step) * step,
			Mathf.Round(euler.Z / step) * step
		);
	}

	private bool ValidateSpecificSnapPosition(Vector3I draggedGridPos, HashSet<Vector3I> occupiedSlots)
	{
		bool isValid = true; 
		bool touchingAny = false;

		if (occupiedSlots != null && occupiedSlots.Contains(draggedGridPos)) return false;

		foreach (Node node in coreGroup.GetChildren())
		{
			if (node is PuzzlePiece p)
			{
				Vector3I pGrid = new Vector3I(
					Mathf.RoundToInt(p.GlobalPosition.X / 2f), 
					Mathf.RoundToInt(p.GlobalPosition.Y / 2f), 
					Mathf.RoundToInt(p.GlobalPosition.Z / 2f)
				);
				
				Vector3I diff = draggedGridPos - pGrid;
				int dist = Mathf.Abs(diff.X) + Mathf.Abs(diff.Y) + Mathf.Abs(diff.Z);

				if (dist == 1) 
				{
					touchingAny = true;
					Vector3 globalDirToCore = new Vector3(-diff.X, -diff.Y, -diff.Z);
					Vector3 globalDirToDragged = new Vector3(diff.X, diff.Y, diff.Z);

					FaceType myFace = selectedPiece.GetFaceInGlobalDirection(globalDirToCore);
					FaceType targetFace = p.GetFaceInGlobalDirection(globalDirToDragged);

					if (!AreFacesCompatible(myFace, targetFace)) 
					{
						isValid = false; 
					}
				}
			}
		}
		return touchingAny && isValid;
	}

	private bool AreFacesCompatible(FaceType face1, FaceType face2)
	{
		if (face1 == FaceType.Flat || face2 == FaceType.Flat) return false;

		if (face1 == FaceType.QuadExt && face2 == FaceType.QuadInt) return true;
		if (face1 == FaceType.QuadInt && face2 == FaceType.QuadExt) return true;
		
		if (face1 == FaceType.CircExt && face2 == FaceType.CircInt) return true;
		if (face1 == FaceType.CircInt && face2 == FaceType.CircExt) return true;
		
		if (face1 == FaceType.CruzExt && face2 == FaceType.CruzInt) return true;
		if (face1 == FaceType.CruzInt && face2 == FaceType.CruzExt) return true;

		return false;
	}
}
