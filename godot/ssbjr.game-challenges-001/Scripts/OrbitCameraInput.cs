using Godot;
using System;

public partial class OrbitCamera
{
	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			// Voar com a Câmara (Botão Direito)
			if (mouseButton.ButtonIndex == MouseButton.Right) 
				isOrbiting = mouseButton.Pressed;
			
			// Zoom (Scroll)
			else if (mouseButton.Pressed && (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown))
				ApplyZoom(mouseButton.ButtonIndex == MouseButton.WheelUp ? -ZoomSpeed : ZoomSpeed);
			
			// PEGAR OU LARGAR LIVREMENTE (Botão Esquerdo)
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

		// === NOVO INPUT EXCLUSIVO PARA ENCAIXAR ===
		if (Input.IsActionJustPressed("join_piece"))
		{
			if (selectedPiece != null && !isHoldingCore && snapGhost.Visible)
			{
				if (isCurrentlyValidSnap) ConfirmJoinPiece();
				else GD.Print("Encaixe inválido! As faces colidem ou são incompatíveis.");
			}
		}

		// Rodar a Peça (Setas)
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
			if (piece.IsAttachedToCore)
			{
				if (isHoldingCore) { DropPieceInPlace(); return; }
				if (selectedPiece != null && !isHoldingCore) DropPieceInPlace();

				isHoldingCore = true;
				selectedPiece = piece; 
				targetPiecePosition = coreGroup.GlobalPosition;
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

	// Larga a peça no mesmo sítio sem a tentar encaixar
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

	// Só é chamado quando carrega na tecla "join_piece" e o fantasma está verde
	private void ConfirmJoinPiece()
	{
		if (selectedPiece != null && isCurrentlyValidSnap)
		{
			selectedPiece.GlobalPosition = currentSnapWorldPos;
			SnapPieceRotationToGrid(selectedPiece);

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
