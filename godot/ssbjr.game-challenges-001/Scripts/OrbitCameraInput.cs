using Godot;
using System;

public partial class OrbitCamera
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right) 
				isOrbiting = mouseButton.Pressed;
			
			else if (mouseButton.Pressed && (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown))
				ApplyZoom(mouseButton.ButtonIndex == MouseButton.WheelUp ? -ZoomSpeed : ZoomSpeed);
			
			else if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
			{
				TrySelectPiece(mouseButton.Position); 
			}
		}
		else if (@event is InputEventMouseMotion mouseMotion && isOrbiting)
		{
			RotateY(-mouseMotion.Relative.X * Sensitivity);
			Vector3 rot = Rotation;
			rot.X = Mathf.Clamp(rot.X - (mouseMotion.Relative.Y * Sensitivity), Mathf.DegToRad(-80), Mathf.DegToRad(80));
			rot.Z = 0;
			Rotation = rot;
		}

		if (Input.IsActionJustPressed("join_piece"))
		{
			if (selectedPiece != null && !isHoldingCore && snapGhost.Visible)
			{
				if (isCurrentlyValidSnap) ConfirmJoinPiece();
				else GD.Print("Encaixe inválido! As faces colidem ou são incompatíveis.");
			}
		}

		Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;
		if (activeNode != null && @event.IsPressed() && !@event.IsEcho())
		{
			float angle90 = Mathf.Pi / 2.0f;
			Vector3 camRight = camera.GlobalTransform.Basis.X.Normalized();
			Vector3 camForward = -camera.GlobalTransform.Basis.Z.Normalized();
			camForward.Y = 0; camForward = camForward.Normalized();

			bool didRotate = false;

			if (Input.IsActionJustPressed("rot_x_pos")) { activeNode.GlobalRotate(camRight, angle90); didRotate = true; }
			if (Input.IsActionJustPressed("rot_x_neg")) { activeNode.GlobalRotate(camRight, -angle90); didRotate = true; }
			if (Input.IsActionJustPressed("rot_y_pos")) { activeNode.GlobalRotate(Vector3.Up, angle90); didRotate = true; }
			if (Input.IsActionJustPressed("rot_y_neg")) { activeNode.GlobalRotate(Vector3.Up, -angle90); didRotate = true; }
			if (Input.IsActionJustPressed("rot_z_pos")) { activeNode.GlobalRotate(camForward, angle90); didRotate = true; }
			if (Input.IsActionJustPressed("rot_z_neg")) { activeNode.GlobalRotate(camForward, -angle90); didRotate = true; }

			if (didRotate) SnapPieceRotationToGrid(activeNode);
		}
	}

	private void TrySelectPiece(Vector2 mousePos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(camera.ProjectRayOrigin(mousePos), camera.ProjectRayOrigin(mousePos) + camera.ProjectRayNormal(mousePos) * 1000f);
		var result = spaceState.IntersectRay(query);

		if (result.Count > 0 && result["collider"].Obj is PuzzlePiece piece)
		{
			// DESLIGA A GRAVIDADE: A peça passa a obedecer exclusivamente aos inputs do jogador
			piece.Freeze = true; 

			if (piece.IsAttachedToCore)
			{
				if (isHoldingCore) { DropPieceInPlace(); return; }
				if (selectedPiece != null && !isHoldingCore) DropPieceInPlace();

				isHoldingCore = true;
				selectedPiece = piece; 
				targetPiecePosition = coreGroup.GlobalPosition;

				// Garante que todas as peças coladas no núcleo também ignoram a física
				foreach (Node n in coreGroup.GetChildren())
				{
					if (n is PuzzlePiece p) p.Freeze = true;
				}
			}
			else
			{
				if (selectedPiece == piece) { DropPieceInPlace(); return; }
				if (selectedPiece != null || isHoldingCore) DropPieceInPlace();

				selectedPiece = piece;
				targetPiecePosition = selectedPiece.GlobalPosition;
				SnapPieceRotationToGrid(selectedPiece);
			}
		}
		else
		{
			if (selectedPiece != null || isHoldingCore) DropPieceInPlace();
		}
	}

	private void DropPieceInPlace()
	{
		if (isHoldingCore)
		{
			Vector3 pos = coreGroup.GlobalPosition;
			coreGroup.GlobalPosition = new Vector3(Mathf.Round(pos.X / 2f) * 2f, Mathf.Round(pos.Y / 2f) * 2f, Mathf.Round(pos.Z / 2f) * 2f);
			SnapPieceRotationToGrid(coreGroup);

			isHoldingCore = false;
			selectedPiece = null; 
			snapGhost.Visible = false;
			return;
		}

		if (selectedPiece != null)
		{
			selectedPiece = null;
			snapGhost.Visible = false;
		}
	}

	private void ConfirmJoinPiece()
	{
		if (selectedPiece != null && isCurrentlyValidSnap)
		{
			selectedPiece.GlobalPosition = currentSnapWorldPos;
			SnapPieceRotationToGrid(selectedPiece);

			// Garante estabilidade física total ao soldar
			selectedPiece.Freeze = true;
			selectedPiece.LinearVelocity = Vector3.Zero;
			selectedPiece.AngularVelocity = Vector3.Zero;
			selectedPiece.IsAttachedToCore = true;
			
			if (coreGroup.GetChildCount() == 0)
			{
				coreGroup.GlobalPosition = currentSnapWorldPos;
				coreGroup.Rotation = Vector3.Zero;
			}

			selectedPiece.Reparent(coreGroup, true);

			isHoldingCore = true; 
			snapGhost.Visible = false;
		}
	}

	private void ApplyZoom(float amount)
	{
		if (camera != null)
		{
			Vector3 camPos = camera.Position;
			camPos.Z = Mathf.Clamp(camPos.Z + amount, MinZoom, MaxZoom);
			camera.Position = camPos;
		}
	}
}
