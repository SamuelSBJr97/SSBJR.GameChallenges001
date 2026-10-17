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

	// FEEDBACK VISUAL
	private MeshInstance3D snapGhost;
	private StandardMaterial3D ghostMat;

	private bool isCurrentlyValidSnap = false;
	private Vector3 currentSnapWorldPos;
	private Vector3 targetPiecePosition;
	private Vector3 targetCameraPosition;

	// INTERFACE DE USUÁRIO (UI)
	private Control uiContainer;
	private Label helpLabel;
	
	// As 6 texturas na tela (0:Up, 1:Down, 2:Right, 3:Left, 4:Forward, 5:Back)
	private TextureRect[] faceIcons = new TextureRect[6]; 
	
	// Texturas carregadas
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;

	public override void _Ready()
	{
		camera = GetNode<Camera3D>("Camera3D");
		targetCameraPosition = GlobalPosition; 
		
		// 1. Cubo Holográfico Aumentado e com Raio-X (NoDepthTest)
		snapGhost = new MeshInstance3D();
		BoxMesh box = new BoxMesh();
		box.Size = new Vector3(2.1f, 2.1f, 2.1f); // 0.1 maior que a peça para a sobrepor visualmente
		snapGhost.Mesh = box;

		ghostMat = new StandardMaterial3D();
		ghostMat.Transparency = BaseMaterial3D.TransparencyEnum.Alpha;
		ghostMat.ShadingMode = BaseMaterial3D.ShadingModeEnum.Unshaded;
		ghostMat.NoDepthTest = true; // Garante que o cubo fantasma é visto ATRAVÉS da peça
		snapGhost.MaterialOverride = ghostMat;
		snapGhost.TopLevel = true;
		snapGhost.Visible = false;
		AddChild(snapGhost);

		coreGroup = new Node3D();
		coreGroup.Name = "CoreAssembly";
		GetParent().CallDeferred(Node.MethodName.AddChild, coreGroup);
		
		LoadTextures();
		BuildUI();
	}
	
	private void LoadTextures()
	{
		// Certifique-se que o caminho confere com onde guardou os PNGs no Godot
		texFlat = GD.Load<Texture2D>("res://Assets/Icons/Flat.png");
		texQuadExt = GD.Load<Texture2D>("res://Assets/Icons/QuadExt.png");
		texQuadInt = GD.Load<Texture2D>("res://Assets/Icons/QuadInt.png");
		texCircExt = GD.Load<Texture2D>("res://Assets/Icons/CircExt.png");
		texCircInt = GD.Load<Texture2D>("res://Assets/Icons/CircInt.png");
		texCruzExt = GD.Load<Texture2D>("res://Assets/Icons/CruzExt.png");
		texCruzInt = GD.Load<Texture2D>("res://Assets/Icons/CruzInt.png");
	}

	private void BuildUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		uiContainer = new Control();
		uiContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		canvas.AddChild(uiContainer);

		// Fundo do minimapa
		ColorRect bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.6f);
		bg.Size = new Vector2(300, 240);
		bg.Position = new Vector2(20, 20);
		uiContainer.AddChild(bg);

		helpLabel = new Label();
		helpLabel.Position = new Vector2(30, 30);
		helpLabel.Text = "Selecione uma peça...";
		helpLabel.LabelSettings = new LabelSettings() { FontSize = 14, FontColor = Colors.White };
		uiContainer.AddChild(helpLabel);

		// Configuração do "Cubo Desdobrado" usando coordenadas 2D (Tamanho do ícone: 50x50)
		float iconSize = 50f;
		float padding = 5f;
		float startX = 30f;
		float startY = 60f;

		// Posições no mapa: [Left] [Forward] [Right] [Back]
		//                          [  Up   ]
		//                          [ Down  ]
		Vector2 posUp = new Vector2(startX + iconSize + padding, startY);
		Vector2 posLeft = new Vector2(startX, startY + iconSize + padding);
		Vector2 posForward = new Vector2(startX + iconSize + padding, startY + iconSize + padding);
		Vector2 posRight = new Vector2(startX + (iconSize + padding) * 2, startY + iconSize + padding);
		Vector2 posBack = new Vector2(startX + (iconSize + padding) * 3, startY + iconSize + padding);
		Vector2 posDown = new Vector2(startX + iconSize + padding, startY + (iconSize + padding) * 2);

		Vector2[] positions = { posUp, posDown, posRight, posLeft, posForward, posBack };

		for (int i = 0; i < 6; i++)
		{
			ColorRect slotBg = new ColorRect(); // Fundo cinza para cada face
			slotBg.Color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
			slotBg.Size = new Vector2(iconSize, iconSize);
			slotBg.Position = positions[i];
			uiContainer.AddChild(slotBg);

			faceIcons[i] = new TextureRect();
			faceIcons[i].ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
			faceIcons[i].Size = new Vector2(iconSize, iconSize);
			faceIcons[i].Position = positions[i];
			uiContainer.AddChild(faceIcons[i]);
		}
	}
	
	private void UpdateUIPanel()
	{
		if (selectedPiece == null)
		{
			helpLabel.Text = "Clique: Selecionar  |  Dir: Câmera";
			foreach (var icon in faceIcons) icon.Texture = null; // Esconde as faces
			return;
		}

		if (isHoldingCore)
		{
			helpLabel.Text = "Movendo Bloco Inteiro. Clique para fixar.";
		}
		else
		{
			helpLabel.Text = "Rotacionar: Setas e PgUp/PgDn";
		}

		// Desenha o minimapa baseado na peça selecionada (solta ou a peça-alvo do grupo)
		faceIcons[0].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Up));
		faceIcons[1].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Down));
		faceIcons[2].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Right));
		faceIcons[3].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Left));
		faceIcons[4].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Forward));
		faceIcons[5].Texture = GetFaceTexture(selectedPiece.GetFaceInGlobalDirection(Vector3.Back));
	}

	private Texture2D GetFaceTexture(FaceType type)
	{
		switch (type)
		{
			case FaceType.QuadExt: return texQuadExt;
			case FaceType.QuadInt: return texQuadInt;
			case FaceType.CircExt: return texCircExt;
			case FaceType.CircInt: return texCircInt;
			case FaceType.CruzExt: return texCruzExt;
			case FaceType.CruzInt: return texCruzInt;
			default: return texFlat;
		}
	}
	
	public override void _Process(double delta)
	{
		// Atualiza o painel da UI a cada frame
		UpdateUIPanel();
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

		// 2. LÓGICA DE SNAPPING DINÂMICO (Busca de Buracos)
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
				Vector3I bestGridPos = Vector3I.Zero;
				float minDistance = float.MaxValue;

				// Mapeia onde já existem peças coladas
				System.Collections.Generic.HashSet<Vector3I> occupiedSlots = new System.Collections.Generic.HashSet<Vector3I>();
				foreach (Node node in coreGroup.GetChildren())
				{
					if (node is PuzzlePiece p)
					{
						occupiedSlots.Add(new Vector3I(Mathf.RoundToInt(p.GlobalPosition.X/2f), Mathf.RoundToInt(p.GlobalPosition.Y/2f), Mathf.RoundToInt(p.GlobalPosition.Z/2f)));
					}
				}

				// Encontra todos os "buracos" vazios vizinhos às peças do Core
				Vector3I[] dirs = { Vector3I.Up, Vector3I.Down, Vector3I.Right, Vector3I.Left, Vector3I.Forward, Vector3I.Back };
				System.Collections.Generic.HashSet<Vector3I> adjacentSlots = new System.Collections.Generic.HashSet<Vector3I>();
				
				foreach (Vector3I occ in occupiedSlots)
				{
					foreach (Vector3I dir in dirs)
					{
						Vector3I adj = occ + dir;
						if (!occupiedSlots.Contains(adj)) adjacentSlots.Add(adj);
					}
				}

				// Procura o buraco que está mais próximo da peça que temos na mão
				foreach (Vector3I adj in adjacentSlots)
				{
					Vector3 worldPos = new Vector3(adj.X * 2f, adj.Y * 2f, adj.Z * 2f);
					float dist = piecePos.DistanceTo(worldPos);
					if (dist < minDistance)
					{
						minDistance = dist;
						bestGridPos = adj;
					}
				}

				// Margem generosa de atração: mesmo que a peça esteja solta no ar, o cubo fantasma liga-se ao buraco!
				if (minDistance <= 3.0f)
				{
					currentSnapWorldPos = new Vector3(bestGridPos.X * 2f, bestGridPos.Y * 2f, bestGridPos.Z * 2f);
					isCurrentlyValidSnap = ValidateSpecificSnapPosition(bestGridPos);

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
				selectedPiece = piece; // CORREÇÃO: Guarda a peça em que clicamos para o minimapa!
				targetPiecePosition = coreGroup.GlobalPosition;
				
				foreach (Node n in coreGroup.GetChildren()) 
					if (n is PuzzlePiece p) p.SetSelected(true);
			}
			else
			{
				if (selectedPiece == piece) { DropOrConfirmPiece(); return; }
				if (selectedPiece != null || isHoldingCore) DropOrConfirmPiece();

				selectedPiece = piece;
				selectedPiece.SetSelected(true);
				targetPiecePosition = selectedPiece.GlobalPosition;
				SnapPieceRotationToGrid(selectedPiece);
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
			selectedPiece = null; // CORREÇÃO: Limpa a referência da UI ao largar o grupo
			snapGhost.Visible = false;
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
		}
	}

	// 3. Ortogonalização Absoluta: Mesmo que a física atire a peça de pernas para o ar,
	// esta função arredonda rigorosamente os ângulos globais para múltiplos perfeitos de 90 graus.
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
