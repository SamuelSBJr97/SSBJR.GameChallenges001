using Godot;
using System;

public partial class OrbitCamera : Node3D
{
	[Export] public float Sensitivity = 0.005f;
	[Export] public float MoveSpeed = 15.0f;
	[Export] public float ZoomSpeed = 3.0f;
	[Export] public float MinZoom = 2.0f;     
	[Export] public float MaxZoom = 200.0f;   

	private bool isOrbiting = false;
	private Camera3D camera;
	
	private PuzzlePiece selectedPiece = null;
	private Node3D coreGroup;       
	private bool isHoldingCore = false; 

	private MeshInstance3D snapGhost;
	private StandardMaterial3D ghostMat;

	private bool isCurrentlyValidSnap = false;
	private Vector3 currentSnapWorldPos;
	private Vector3 targetPiecePosition;
	private Vector3 targetCameraPosition;

	// INTERFACE DE USUÁRIO (Minimapa 3D)
	private Label helpLabel;
	private SubViewport minimapViewport;
	private Camera3D minimapCamera;
	private Node3D minimapRoot;

	public override void _Ready()
	{
		camera = GetNode<Camera3D>("Camera3D");
		targetCameraPosition = GlobalPosition; 
		
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

		coreGroup = new Node3D();
		coreGroup.Name = "CoreAssembly";
		GetParent().CallDeferred(Node.MethodName.AddChild, coreGroup);

		Build3DMinimapUI();
	}

	private void Build3DMinimapUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		helpLabel = new Label();
		helpLabel.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		helpLabel.Position = new Vector2(20, 20);
		helpLabel.LabelSettings = new LabelSettings() { FontSize = 16, FontColor = Colors.White };
		canvas.AddChild(helpLabel);

		// Contentor da Miniatura 3D (Canto Superior Direito)
		SubViewportContainer container = new SubViewportContainer();
		container.SetAnchorsPreset(Control.LayoutPreset.TopRight);
		container.Position = new Vector2(-280, 20); // 20px de margem
		container.Size = new Vector2(250, 250);
		canvas.AddChild(container);

		// Fundo escuro para a miniatura
		ColorRect bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.5f);
		bg.Size = new Vector2(250, 250);
		container.AddChild(bg);

		// Cria o Mundo 3D isolado para a UI
		minimapViewport = new SubViewport();
		minimapViewport.TransparentBg = true;
		minimapViewport.Size = new Vector2I(250, 250);
		minimapViewport.OwnWorld3D = true; // Isola as luzes e sombras do minimapa do resto do jogo
		container.AddChild(minimapViewport);

		minimapRoot = new Node3D();
		minimapViewport.AddChild(minimapRoot);

		minimapCamera = new Camera3D();
		minimapViewport.AddChild(minimapCamera);

		// Luzes exclusivas do minimapa
		DirectionalLight3D light1 = new DirectionalLight3D();
		light1.Position = new Vector3(5, 5, 5);
		light1.LookAt(Vector3.Zero);
		minimapViewport.AddChild(light1);

		DirectionalLight3D light2 = new DirectionalLight3D();
		light2.Position = new Vector3(-5, 5, -5);
		light2.LookAt(Vector3.Zero);
		minimapViewport.AddChild(light2);
	}

	// O Motor de Clonagem: Copia as malhas 3D para o minimapa
	private void RefreshMinimap()
	{
		// Limpa a miniatura antiga
		foreach (Node n in minimapRoot.GetChildren()) n.QueueFree();

		Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;
		if (activeNode == null) return;

		// Clona a topologia atual preservando as posições relativas
		Transform3D invTransform = activeNode.GlobalTransform.AffineInverse();
		CloneMeshes(activeNode, minimapRoot, invTransform);
	}

	private void CloneMeshes(Node current, Node targetParent, Transform3D invTransform)
	{
		if (current is MeshInstance3D meshInst)
		{
			MeshInstance3D clone = new MeshInstance3D();
			clone.Mesh = meshInst.Mesh;
			
			// Mantém as cores originais da peça
			if (meshInst.MaterialOverride != null) clone.MaterialOverride = meshInst.MaterialOverride;
			for (int i = 0; i < meshInst.GetSurfaceOverrideMaterialCount(); i++)
				clone.SetSurfaceOverrideMaterial(i, meshInst.GetSurfaceOverrideMaterial(i));

			clone.Transform = invTransform * meshInst.GlobalTransform;
			targetParent.AddChild(clone);
		}

		foreach (Node child in current.GetChildren())
			CloneMeshes(child, targetParent, invTransform);
	}

	public override void _Process(double delta)
	{
		Node3D activeNode = isHoldingCore ? coreGroup : selectedPiece;

		if (activeNode == null)
		{
			helpLabel.Text = "=== CONTROLES ===\nClique Esq: Selecionar\nDir: Voar Câmera";
			minimapRoot.Visible = false;
		}
		else
		{
			helpLabel.Text = isHoldingCore ? "=== MOVENDO BLOCO (Clique para fixar) ===" : "=== ROTACIONAR (Setas / PgUp / PgDn) ===";
			minimapRoot.Visible = true;

			// Sincroniza a Rotação 3D do minimapa com a peça
			minimapRoot.Basis = activeNode.GlobalTransform.Basis;

			// Coloca a câmara do minimapa no mesmo ângulo da câmara principal para a perspetiva ser idêntica
			Vector3 dirToCam = (camera.GlobalPosition - activeNode.GlobalPosition).Normalized();
			
			// Afasta a câmara dependendo do tamanho do grupo (para não cortar as peças)
			float zoomDist = isHoldingCore ? 8.0f + (coreGroup.GetChildCount() * 1.5f) : 6.0f;
			
			minimapCamera.Position = dirToCam * zoomDist;
			minimapCamera.LookAt(Vector3.Zero);
		}
	}

	public override void _UnhandledInput(InputEvent @event)
	{
		if (@event is InputEventMouseButton mouseButton)
		{
			if (mouseButton.ButtonIndex == MouseButton.Right) isOrbiting = mouseButton.Pressed;
			else if (mouseButton.Pressed && (mouseButton.ButtonIndex == MouseButton.WheelUp || mouseButton.ButtonIndex == MouseButton.WheelDown))
				ApplyZoom(mouseButton.ButtonIndex == MouseButton.WheelUp ? -ZoomSpeed : ZoomSpeed);
			else if (mouseButton.ButtonIndex == MouseButton.Left && mouseButton.Pressed)
				TrySelectPiece(mouseButton.Position);
		}
		else if (@event is InputEventMouseMotion mouseMotion && isOrbiting)
		{
			RotateY(-mouseMotion.Relative.X * Sensitivity);
			Vector3 rot = Rotation;
			rot.X = Mathf.Clamp(rot.X - (mouseMotion.Relative.Y * Sensitivity), Mathf.DegToRad(-80), Mathf.DegToRad(80));
			rot.Z = 0;
			Rotation = rot;
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

	public override void _PhysicsProcess(double delta)
	{
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
				Vector3 bestWorldPos = Vector3.Zero;
				float minDistance = 4.0f;
				isCurrentlyValidSnap = false;
				bool foundSnap = false;

				Vector3[] dirs = { Vector3.Up * 2f, Vector3.Down * 2f, Vector3.Right * 2f, Vector3.Left * 2f, Vector3.Forward * 2f, Vector3.Back * 2f };
				
				foreach (Node node in coreGroup.GetChildren())
				{
					if (node is PuzzlePiece p)
					{
						foreach (Vector3 d in dirs)
						{
							Vector3 checkPos = p.GlobalPosition + d;
							float dist = piecePos.DistanceTo(checkPos);
							
							if (dist < minDistance)
							{
								Vector3I checkGrid = new Vector3I(Mathf.RoundToInt(checkPos.X/2f), Mathf.RoundToInt(checkPos.Y/2f), Mathf.RoundToInt(checkPos.Z/2f));
								bool isValid = ValidateSpecificSnapPosition(checkGrid);
								
								minDistance = dist;
								bestWorldPos = checkPos;
								isCurrentlyValidSnap = isValid;
								foundSnap = true;
								currentSnapWorldPos = bestWorldPos;
							}
						}
					}
				}

				if (foundSnap)
				{
					snapGhost.GlobalPosition = currentSnapWorldPos;
					snapGhost.Visible = true;
					ghostMat.AlbedoColor = isCurrentlyValidSnap ? new Color(0, 1, 0, 0.4f) : new Color(1, 0, 0, 0.4f);

					if (isCurrentlyValidSnap && !isOrbiting)
					{
						selectedPiece.GlobalPosition = selectedPiece.GlobalPosition.Lerp(currentSnapWorldPos, (float)delta * 20f);
						targetPiecePosition = selectedPiece.GlobalPosition; 
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

	private void TrySelectPiece(Vector2 mousePos)
	{
		var spaceState = GetWorld3D().DirectSpaceState;
		var query = PhysicsRayQueryParameters3D.Create(camera.ProjectRayOrigin(mousePos), camera.ProjectRayOrigin(mousePos) + camera.ProjectRayNormal(mousePos) * 1000f);
		var result = spaceState.IntersectRay(query);

		if (result.Count > 0 && result["collider"].Obj is PuzzlePiece piece)
		{
			if (piece.IsAttachedToCore)
			{
				if (isHoldingCore) { DropOrConfirmPiece(); return; }
				if (selectedPiece != null && !isHoldingCore) DropOrConfirmPiece();

				isHoldingCore = true;
				selectedPiece = piece; 
				targetPiecePosition = coreGroup.GlobalPosition;
				
				foreach (Node n in coreGroup.GetChildren()) 
					if (n is PuzzlePiece p) p.SetSelected(true);
				
				RefreshMinimap(); // Atualiza a miniatura 3D para o Grupo
			}
			else
			{
				if (selectedPiece == piece) { DropOrConfirmPiece(); return; }
				if (selectedPiece != null || isHoldingCore) DropOrConfirmPiece();

				selectedPiece = piece;
				selectedPiece.SetSelected(true);
				targetPiecePosition = selectedPiece.GlobalPosition;
				SnapPieceRotationToGrid(selectedPiece);
				
				RefreshMinimap(); // Atualiza a miniatura 3D para a Peça individual
			}
		}
		else
		{
			if (selectedPiece != null || isHoldingCore) DropOrConfirmPiece();
		}
	}

	private void DropOrConfirmPiece()
	{
		if (isHoldingCore)
		{
			Vector3 pos = coreGroup.GlobalPosition;
			coreGroup.GlobalPosition = new Vector3(Mathf.Round(pos.X / 2f) * 2f, Mathf.Round(pos.Y / 2f) * 2f, Mathf.Round(pos.Z / 2f) * 2f);
			SnapPieceRotationToGrid(coreGroup);

			foreach (Node n in coreGroup.GetChildren()) 
				if (n is PuzzlePiece p) p.SetSelected(false);
			
			isHoldingCore = false;
			selectedPiece = null; 
			snapGhost.Visible = false;
			RefreshMinimap(); // Limpa a miniatura
			return;
		}

		if (selectedPiece != null)
		{
			if (isCurrentlyValidSnap)
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
			}

			selectedPiece.SetSelected(false);
			selectedPiece = null;
			snapGhost.Visible = false;
			RefreshMinimap(); // Limpa a miniatura
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

	private bool ValidateSpecificSnapPosition(Vector3I draggedGridPos)
	{
		bool isValid = true; 
		bool touchingAny = false;

		foreach (Node node in coreGroup.GetChildren())
		{
			if (node is PuzzlePiece p)
			{
				Vector3I pGrid = new Vector3I(Mathf.RoundToInt(p.GlobalPosition.X / 2f), Mathf.RoundToInt(p.GlobalPosition.Y / 2f), Mathf.RoundToInt(p.GlobalPosition.Z / 2f));
				Vector3I diff = draggedGridPos - pGrid;
				int dist = Mathf.Abs(diff.X) + Mathf.Abs(diff.Y) + Mathf.Abs(diff.Z);

				if (dist == 1)
				{
					touchingAny = true;
					Vector3 globalDirToCore = new Vector3(-diff.X, -diff.Y, -diff.Z);
					Vector3 globalDirToDragged = new Vector3(diff.X, diff.Y, diff.Z);

					FaceType myFace = selectedPiece.GetFaceInGlobalDirection(globalDirToCore);
					FaceType targetFace = p.GetFaceInGlobalDirection(globalDirToDragged);

					if (myFace == FaceType.Flat || targetFace == FaceType.Flat || (int)myFace + (int)targetFace != 0)
						isValid = false; 
				}
			}
		}
		return touchingAny && isValid;
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
