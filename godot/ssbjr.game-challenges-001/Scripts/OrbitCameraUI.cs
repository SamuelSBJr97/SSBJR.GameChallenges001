using Godot;
using System.Collections.Generic;

public partial class OrbitCamera
{
	private Control dynamicUIContainer;
	private Label helpLabel;
	
	// Texturas
	private Texture2D texFlat, texQuadExt, texQuadInt, texCircExt, texCircInt, texCruzExt, texCruzInt;

	// Sistema de Pool de Performance para não criar/destruir nós a cada frame
	private List<TextureRect> iconPool = new List<TextureRect>();
	private int activeIconsCount = 0;

	private void LoadTextures()
	{
		texFlat = GD.Load<Texture2D>("res://UI/Icons/Flat.png");
		texQuadExt = GD.Load<Texture2D>("res://UI/Icons/QuadExt.png");
		texQuadInt = GD.Load<Texture2D>("res://UI/Icons/QuadInt.png");
		texCircExt = GD.Load<Texture2D>("res://UI/Icons/CircExt.png");
		texCircInt = GD.Load<Texture2D>("res://UI/Icons/CircInt.png");
		texCruzExt = GD.Load<Texture2D>("res://UI/Icons/CruzExt.png");
		texCruzInt = GD.Load<Texture2D>("res://UI/Icons/CruzInt.png");
	}

	private void Build2DMinimapUI()
	{
		CanvasLayer canvas = new CanvasLayer();
		AddChild(canvas);

		dynamicUIContainer = new Control();
		dynamicUIContainer.SetAnchorsPreset(Control.LayoutPreset.TopLeft);
		canvas.AddChild(dynamicUIContainer);

		ColorRect bg = new ColorRect();
		bg.Color = new Color(0, 0, 0, 0.75f);
		bg.Size = new Vector2(400, 320); // Aumentado para acomodar blocos grandes
		bg.Position = new Vector2(20, 20);
		dynamicUIContainer.AddChild(bg);

		helpLabel = new Label();
		helpLabel.Position = new Vector2(30, 30);
		helpLabel.LabelSettings = new LabelSettings() { FontSize = 14, FontColor = Colors.White };
		dynamicUIContainer.AddChild(helpLabel);
	}

	private TextureRect GetIconFromPool()
	{
		if (activeIconsCount < iconPool.Count)
		{
			TextureRect icon = iconPool[activeIconsCount];
			icon.Visible = true;
			activeIconsCount++;
			return icon;
		}

		TextureRect newIcon = new TextureRect();
		newIcon.ExpandMode = TextureRect.ExpandModeEnum.IgnoreSize;
		
		// Fundo subtil para ajudar a ver os buracos na topologia
		ColorRect bg = new ColorRect();
		bg.Color = new Color(0.2f, 0.2f, 0.2f, 0.8f);
		bg.SetAnchorsPreset(Control.LayoutPreset.FullRect);
		bg.ShowBehindParent = true;
		newIcon.AddChild(bg);

		dynamicUIContainer.AddChild(newIcon);
		iconPool.Add(newIcon);
		activeIconsCount++;
		return newIcon;
	}

	private void HideUnusedIcons()
	{
		for (int i = activeIconsCount; i < iconPool.Count; i++)
		{
			iconPool[i].Visible = false;
		}
	}

	private void UpdateUIProcess()
	{
		activeIconsCount = 0; // Prepara o pool para o frame atual

		if (selectedPiece == null && !isHoldingCore)
		{
			helpLabel.Text = "Clique: Selecionar  |  Dir: Câmera";
			HideUnusedIcons();
			return;
		}

		helpLabel.Text = isHoldingCore 
			? "=== MOVENDO BLOCO (Topologia Completa) ===" 
			: "=== ROTACIONAR (Setas / PgUp / PgDn) ===";

		List<PuzzlePiece> activePieces = new List<PuzzlePiece>();
		if (isHoldingCore)
		{
			foreach (Node n in coreGroup.GetChildren())
				if (n is PuzzlePiece p) activePieces.Add(p);
		}
		else
		{
			activePieces.Add(selectedPiece);
		}

		// Calcula a Direção Absoluta Dominante da Câmara (Para fixar o mapa em eixos ortogonais limpos)
		Vector3 camFront = GetDominantAxis(-camera.GlobalTransform.Basis.Z);
		camFront.Y = 0; if (camFront == Vector3.Zero) camFront = Vector3.Forward;
		camFront = camFront.Normalized();

		Vector3 camRight = GetDominantAxis(camera.GlobalTransform.Basis.X);
		camRight.Y = 0; if (camRight == Vector3.Zero) camRight = Vector3.Right;
		camRight = camRight.Normalized();

		Vector3 camUp = Vector3.Up;
		Vector3 camDown = Vector3.Down;
		Vector3 camLeft = -camRight;
		Vector3 camBack = -camFront;

		// Configuração dos Centros da Grelha na UI (Tamanho do painel: 400x320)
		float cx = 150f; 
		float cy = 160f;
		float spacing = 80f; // Distância entre as secções da cruz
		float iconSize = 24f; // Tamanho de cada face na topologia

		// Desenha a topologia de cada direção (Projetando o bloco 3D em eixos 2D)
		// [Frente da UI] -> Apontada para a Câmara (camFront)
		DrawTopologicalView(camFront, camRight, camUp, new Vector2(cx, cy), activePieces, iconSize);
		// [Trás]
		DrawTopologicalView(camBack, camLeft, camUp, new Vector2(cx + spacing * 3, cy), activePieces, iconSize);
		// [Direita]
		DrawTopologicalView(camRight, camBack, camUp, new Vector2(cx + spacing, cy), activePieces, iconSize);
		// [Esquerda]
		DrawTopologicalView(camLeft, camFront, camUp, new Vector2(cx - spacing, cy), activePieces, iconSize);
		// [Cima]
		DrawTopologicalView(camUp, camRight, camBack, new Vector2(cx, cy - spacing), activePieces, iconSize);
		// [Baixo]
		DrawTopologicalView(camDown, camRight, camFront, new Vector2(cx, cy + spacing), activePieces, iconSize);

		HideUnusedIcons();
	}

	// Projeta o bloco 3D numa malha 2D baseada na direção da vista
	private void DrawTopologicalView(Vector3 viewDir, Vector3 layoutRight, Vector3 layoutUp, Vector2 centerScreenPos, List<PuzzlePiece> pieces, float iconSize)
	{
		// Dicionário: Coordenada 2D na grelha -> (Profundidade, Peça)
		Dictionary<Vector2I, (float depth, PuzzlePiece piece)> projection = new Dictionary<Vector2I, (float, PuzzlePiece)>();

		foreach (var p in pieces)
		{
			Vector3 pos = p.GlobalPosition;
			// Converte a posição do mundo 3D em coordenadas da grelha local para esta vista
			int x = Mathf.RoundToInt(pos.Dot(layoutRight) / 2f);
			int y = Mathf.RoundToInt(pos.Dot(layoutUp) / 2f);
			float depth = pos.Dot(viewDir); // O quão perto está da câmara desta direção

			Vector2I coord = new Vector2I(x, y);
			
			// Só guardamos a face que estiver mais à frente (maior depth) na topologia!
			if (!projection.ContainsKey(coord) || projection[coord].depth < depth)
			{
				projection[coord] = (depth, p);
			}
		}

		// Calcula a Bounding Box 2D para centrar a silhueta gerada no ecrã
		float minX = float.MaxValue, maxX = float.MinValue;
		float minY = float.MaxValue, maxY = float.MinValue;

		foreach (var coord in projection.Keys)
		{
			if (coord.X < minX) minX = coord.X;
			if (coord.X > maxX) maxX = coord.X;
			if (coord.Y < minY) minY = coord.Y;
			if (coord.Y > maxY) maxY = coord.Y;
		}

		float offsetX = -(maxX + minX) / 2f;
		float offsetY = -(maxY + minY) / 2f;

		// Desenha a topologia final
		foreach (var kvp in projection)
		{
			Vector2I coord = kvp.Key;
			PuzzlePiece piece = kvp.Value.piece;

			// Calcula a posição no ecrã (O Godot Y desce no ecrã, mas o nosso layoutUp sobe no mundo, por isso o sinal negativo)
			float screenX = centerScreenPos.X + (coord.X + offsetX) * iconSize - iconSize / 2f;
			float screenY = centerScreenPos.Y - (coord.Y + offsetY) * iconSize - iconSize / 2f;

			TextureRect icon = GetIconFromPool();
			icon.Position = new Vector2(screenX, screenY);
			icon.Size = new Vector2(iconSize, iconSize);
			
			// Vai buscar a face verdadeira daquela peça virada para a direção especificada
			FaceType face = piece.GetFaceInGlobalDirection(viewDir);
			icon.Texture = GetFaceTexture(face);
		}
	}

	private Vector3 GetDominantAxis(Vector3 v)
	{
		Vector3 absV = new Vector3(Mathf.Abs(v.X), Mathf.Abs(v.Y), Mathf.Abs(v.Z));
		if (absV.X > absV.Y && absV.X > absV.Z) return new Vector3(Mathf.Sign(v.X), 0, 0);
		if (absV.Y > absV.X && absV.Y > absV.Z) return new Vector3(0, Mathf.Sign(v.Y), 0);
		return new Vector3(0, 0, Mathf.Sign(v.Z));
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
}
