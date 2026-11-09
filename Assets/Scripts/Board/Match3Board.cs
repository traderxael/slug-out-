using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using SlugOut.Battle;
using SlugOut.Core;

namespace SlugOut.Board
{
    public class Match3Board : MonoBehaviour
    {
        [Header("Dimensiones del Tablero")]
        [SerializeField] private int columns = 6;
        [SerializeField] private int rows = 5;
        [SerializeField] private float tileSpacing = 80f;

        [Header("Referencias")]
        [SerializeField] private GameObject tilePrefab;
        [SerializeField] private RectTransform boardContainer;
        [SerializeField] private Duelist playerDuelist;

        [Header("Audio")]
        [SerializeField] private AudioSource audioSource;
        [SerializeField] private AudioClip matchSound;
        [SerializeField] private AudioClip swapSound;

        private BoardTile[,] _grid;
        private bool _isProcessingMatches = false;
        private BoardTile _selectedTile = null;

        public event Action<TileType, int> OnMatchMade; // tipo, cantidad

        private void Start()
        {
            if (boardContainer == null)
            {
                boardContainer = GetComponent<RectTransform>();
            }

            InitializeGrid();
        }

        public void InitializeGrid()
        {
            // Limpiar anteriores
            if (_grid != null)
            {
                foreach (var tile in _grid)
                {
                    if (tile != null) Destroy(tile.gameObject);
                }
            }

            _grid = new BoardTile[columns, rows];

            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    TileType type = GetRandomNonMatchingType(x, y);
                    CreateTile(x, y, type);
                }
            }
        }

        private TileType GetRandomNonMatchingType(int x, int y)
        {
            var types = new List<TileType>((TileType[])Enum.GetValues(typeof(TileType)));

            // Evitar matches horizontales al inicio
            if (x >= 2 && _grid[x - 1, y] != null && _grid[x - 2, y] != null)
            {
                if (_grid[x - 1, y].tileType == _grid[x - 2, y].tileType)
                {
                    types.Remove(_grid[x - 1, y].tileType);
                }
            }

            // Evitar matches verticales al inicio
            if (y >= 2 && _grid[x, y - 1] != null && _grid[x, y - 2] != null)
            {
                if (_grid[x, y - 1].tileType == _grid[x, y - 2].tileType)
                {
                    types.Remove(_grid[x, y - 1].tileType);
                }
            }

            return types[UnityEngine.Random.Range(0, types.Count)];
        }

        private BoardTile CreateTile(int x, int y, TileType type)
        {
            GameObject go;
            if (tilePrefab != null)
            {
                go = Instantiate(tilePrefab, boardContainer);
            }
            else
            {
                go = new GameObject($"Tile_{x}_{y}", typeof(RectTransform), typeof(Image));
                go.transform.SetParent(boardContainer, false);
                var img = go.GetComponent<Image>();
                img.color = TileHelper.GetTileColor(type);
            }

            BoardTile tile = go.GetComponent<BoardTile>();
            if (tile == null)
            {
                tile = go.AddComponent<BoardTile>();
            }

            RectTransform rt = go.GetComponent<RectTransform>();
            rt.sizeDelta = new Vector2(tileSpacing * 0.9f, tileSpacing * 0.9f);
            rt.anchoredPosition = GetAnchoredPosition(x, y);

            tile.Initialize(x, y, type);
            tile.OnSwipeDetected += HandleSwipe;
            tile.OnTileSelected += HandleTileSelect;

            _grid[x, y] = tile;
            return tile;
        }

        private Vector2 GetAnchoredPosition(int x, int y)
        {
            float totalW = (columns - 1) * tileSpacing;
            float totalH = (rows - 1) * tileSpacing;
            float startX = -totalW * 0.5f;
            float startY = -totalH * 0.5f;

            return new Vector2(startX + x * tileSpacing, startY + y * tileSpacing);
        }

        private void HandleTileSelect(BoardTile tile)
        {
            if (_isProcessingMatches) return;

            if (_selectedTile == null)
            {
                _selectedTile = tile;
            }
            else
            {
                // Si selecciono una vecina, intentar intercambiar
                if (IsNeighbor(_selectedTile, tile))
                {
                    StartCoroutine(SwapAndEvaluate(_selectedTile, tile));
                }
                _selectedTile = null;
            }
        }

        private void HandleSwipe(BoardTile tile, Vector2 direction)
        {
            if (_isProcessingMatches) return;

            int targetX = tile.column + Mathf.RoundToInt(direction.x);
            int targetY = tile.row + Mathf.RoundToInt(direction.y);

            if (targetX >= 0 && targetX < columns && targetY >= 0 && targetY < rows)
            {
                BoardTile target = _grid[targetX, targetY];
                if (target != null)
                {
                    StartCoroutine(SwapAndEvaluate(tile, target));
                }
            }
        }

        private bool IsNeighbor(BoardTile a, BoardTile b)
        {
            int dx = Mathf.Abs(a.column - b.column);
            int dy = Mathf.Abs(a.row - b.row);
            return (dx == 1 && dy == 0) || (dx == 0 && dy == 1);
        }

        private IEnumerator SwapAndEvaluate(BoardTile a, BoardTile b)
        {
            _isProcessingMatches = true;

            // Intercambiar en matriz
            int ax = a.column, ay = a.row;
            int bx = b.column, by = b.row;

            _grid[ax, ay] = b;
            _grid[bx, by] = a;

            a.column = bx; a.row = by;
            b.column = ax; b.row = ay;

            // Animar movimiento
            yield return StartCoroutine(AnimateSwap(a, b));

            // Evaluar si genera coincidencia
            List<BoardTile> matches = FindAllMatches();

            if (matches.Count > 0)
            {
                // Exito de match
                yield return StartCoroutine(ProcessMatchesRoutine(matches));
            }
            else
            {
                // Revertir intercambio
                _grid[ax, ay] = a;
                _grid[bx, by] = b;

                a.column = ax; a.row = ay;
                b.column = bx; b.row = by;

                yield return StartCoroutine(AnimateSwap(a, b));
            }

            _isProcessingMatches = false;
        }

        private IEnumerator AnimateSwap(BoardTile a, BoardTile b)
        {
            Vector2 posA = GetAnchoredPosition(a.column, a.row);
            Vector2 posB = GetAnchoredPosition(b.column, b.row);

            RectTransform rtA = a.GetComponent<RectTransform>();
            RectTransform rtB = b.GetComponent<RectTransform>();

            float duration = 0.15f;
            float elapsed = 0f;
            Vector2 startA = rtA.anchoredPosition;
            Vector2 startB = rtB.anchoredPosition;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = elapsed / duration;
                rtA.anchoredPosition = Vector2.Lerp(startA, posA, t);
                rtB.anchoredPosition = Vector2.Lerp(startB, posB, t);
                yield return null;
            }

            rtA.anchoredPosition = posA;
            rtB.anchoredPosition = posB;
        }

        private List<BoardTile> FindAllMatches()
        {
            HashSet<BoardTile> matchedTiles = new HashSet<BoardTile>();

            // Horizontal
            for (int y = 0; y < rows; y++)
            {
                for (int x = 0; x < columns - 2; x++)
                {
                    BoardTile t1 = _grid[x, y];
                    BoardTile t2 = _grid[x + 1, y];
                    BoardTile t3 = _grid[x + 2, y];

                    if (t1 != null && t2 != null && t3 != null)
                    {
                        if (t1.tileType == t2.tileType && t2.tileType == t3.tileType)
                        {
                            matchedTiles.Add(t1);
                            matchedTiles.Add(t2);
                            matchedTiles.Add(t3);
                        }
                    }
                }
            }

            // Vertical
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows - 2; y++)
                {
                    BoardTile t1 = _grid[x, y];
                    BoardTile t2 = _grid[x, y + 1];
                    BoardTile t3 = _grid[x, y + 2];

                    if (t1 != null && t2 != null && t3 != null)
                    {
                        if (t1.tileType == t2.tileType && t2.tileType == t3.tileType)
                        {
                            matchedTiles.Add(t1);
                            matchedTiles.Add(t2);
                            matchedTiles.Add(t3);
                        }
                    }
                }
            }

            return new List<BoardTile>(matchedTiles);
        }

        private IEnumerator ProcessMatchesRoutine(List<BoardTile> matches)
        {
            while (matches.Count > 0)
            {
                // Contabilizar energia segun tipo de fichas
                Dictionary<TileType, int> counts = new Dictionary<TileType, int>();
                foreach (var tile in matches)
                {
                    if (!counts.ContainsKey(tile.tileType)) counts[tile.tileType] = 0;
                    counts[tile.tileType]++;
                }

                // Aplicar recompensas de energia / curacion al duelista
                foreach (var kvp in counts)
                {
                    ApplyMatchReward(kvp.Key, kvp.Value);
                    OnMatchMade?.Invoke(kvp.Key, kvp.Value);
                }

                // Destruir fichas coincidentes
                foreach (var tile in matches)
                {
                    _grid[tile.column, tile.row] = null;
                    Destroy(tile.gameObject);
                }

                yield return new WaitForSeconds(0.12f);

                // Hacer caer fichas y rellenar
                yield return StartCoroutine(DropAndFillGrid());

                // Verificar si hay casadas / combos
                matches = FindAllMatches();
                yield return new WaitForSeconds(0.1f);
            }
        }

        private void ApplyMatchReward(TileType type, int count)
        {
            if (playerDuelist == null) return;

            float energyMultiplier = 25f; // 25 de energia por ficha

            if (type == TileType.Heart)
            {
                playerDuelist.Heal(12f * count);
            }
            else if (type == TileType.EnergyBoost)
            {
                for (int i = 0; i < playerDuelist.EquippedCount; i++)
                {
                    playerDuelist.AddEnergyToSlot(i, 10f * count);
                }
            }
            else
            {
                ElementType elem = TileHelper.ToElementType(type);
                playerDuelist.AddEnergyByElement(elem, energyMultiplier * count);
            }
        }

        private IEnumerator DropAndFillGrid()
        {
            // Caida de fichas existentes hacia abajo
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (_grid[x, y] == null)
                    {
                        // Buscar una ficha arriba
                        for (int aboveY = y + 1; aboveY < rows; aboveY++)
                        {
                            if (_grid[x, aboveY] != null)
                            {
                                BoardTile fallingTile = _grid[x, aboveY];
                                _grid[x, y] = fallingTile;
                                _grid[x, aboveY] = null;
                                fallingTile.row = y;

                                RectTransform rt = fallingTile.GetComponent<RectTransform>();
                                rt.anchoredPosition = GetAnchoredPosition(x, y);
                                break;
                            }
                        }
                    }
                }
            }

            // Generar nuevas fichas arriba para los huecos restantes
            for (int x = 0; x < columns; x++)
            {
                for (int y = 0; y < rows; y++)
                {
                    if (_grid[x, y] == null)
                    {
                        var allTypes = (TileType[])Enum.GetValues(typeof(TileType));
                        TileType newType = allTypes[UnityEngine.Random.Range(0, allTypes.Length)];
                        CreateTile(x, y, newType);
                    }
                }
            }

            yield return new WaitForSeconds(0.15f);
        }
    }
}
