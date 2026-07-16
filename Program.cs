using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;

namespace TetrisConsole
{
    // Класс, представляющий падающую фигуру
    public class Figure
    {
        // Матрица битов фигуры (true – есть блок, false – пусто)
        public bool[,] Shape { get; private set; }
        // Позиция левого верхнего угла ограничивающей рамки фигуры на поле
        public int X { get; set; }
        public int Y { get; set; }

        public int Height => Shape.GetLength(0);
        public int Width => Shape.GetLength(1);

        public Figure(bool[,] shape, int x, int y)
        {
            Shape = shape;
            X = x;
            Y = y;
        }

        // Движение влево
        public bool MoveLeft(Field field)
        {
            X--;
            if (field.IsValidPosition(this)) return true;
            X++;
            return false;
        }

        // Движение вправо
        public bool MoveRight(Field field)
        {
            X++;
            if (field.IsValidPosition(this)) return true;
            X--;
            return false;
        }

        // Движение вниз
        public bool MoveDown(Field field)
        {
            Y++;
            if (field.IsValidPosition(this)) return true;
            Y--;
            return false;
        }

        // Поворот на 90 градусов по часовой стрелке с сохранением визуального центра
        public bool Rotate(Field field)
        {
            int oldH = Height, oldW = Width;
            int newH = oldW, newW = oldH;
            bool[,] rotated = new bool[newH, newW];

            // Поворот матрицы
            for (int i = 0; i < oldH; i++)
                for (int j = 0; j < oldW; j++)
                    rotated[j, oldH - 1 - i] = Shape[i, j];

            // Вычисляем центр старой фигуры (в координатах относительно рамки)
            float oldCenterX = 0, oldCenterY = 0;
            int count = 0;
            for (int i = 0; i < oldH; i++)
                for (int j = 0; j < oldW; j++)
                    if (Shape[i, j])
                    {
                        oldCenterX += j + 0.5f;
                        oldCenterY += i + 0.5f;
                        count++;
                    }
            if (count == 0) return false;
            oldCenterX /= count;
            oldCenterY /= count;

            // Центр новой повёрнутой фигуры
            float newCenterX = 0, newCenterY = 0;
            for (int i = 0; i < newH; i++)
                for (int j = 0; j < newW; j++)
                    if (rotated[i, j])
                    {
                        newCenterX += j + 0.5f;
                        newCenterY += i + 0.5f;
                    }
            newCenterX /= count;
            newCenterY /= count;

            // Корректирует координаты так, чтобы визуальный центр остался на месте
            int newX = X + (int)Math.Round(oldCenterX - newCenterX);
            int newY = Y + (int)Math.Round(oldCenterY - newCenterY);

            // Временная фигура для проверки коллизий
            Figure temp = new Figure(rotated, newX, newY);
            if (field.IsValidPosition(temp))
            {
                Shape = rotated;
                X = newX;
                Y = newY;
                return true;
            }
            return false;
        }
    }

    // Класс игрового поля
    public class Field
    {
        public const int Width = 10;   // ширина поля в блоках
        public const int Height = 20;  // высота поля в блоках

        private int[,] grid = new int[Height, Width]; // 0 – пусто, 1 – занято

        // Проверка, находится ли клетка внутри поля
        public bool IsInside(int x, int y)
        {
            return x >= 0 && x < Width && y >= 0 && y < Height;
        }

        // Занята ли клетка поля
        public bool IsOccupied(int x, int y)
        {
            return IsInside(x, y) && grid[y, x] != 0;
        }

        // Проверка корректности позиции всей фигуры
        public bool IsValidPosition(Figure fig)
        {
            for (int i = 0; i < fig.Height; i++)
                for (int j = 0; j < fig.Width; j++)
                {
                    if (fig.Shape[i, j])
                    {
                        int absX = fig.X + j;
                        int absY = fig.Y + i;
                        // Выход за левую, правую или нижнюю границу
                        if (absX < 0 || absX >= Width || absY >= Height)
                            return false;
                        // Пересечение с уже зафиксированными блоками (только внутри поля)
                        if (absY >= 0 && grid[absY, absX] != 0)
                            return false;
                    }
                }
            return true;
        }

        // Фиксация фигуры на поле
        public void FixFigure(Figure fig)
        {
            for (int i = 0; i < fig.Height; i++)
                for (int j = 0; j < fig.Width; j++)
                    if (fig.Shape[i, j])
                    {
                        int absX = fig.X + j;
                        int absY = fig.Y + i;
                        if (IsInside(absX, absY))
                            grid[absY, absX] = 1;
                    }
        }

        // Удаление заполненных линий, сдвиг верхних вниз, возвращает количество убранных линий
        public int ClearLines()
        {
            int lines = 0;
            for (int y = Height - 1; y >= 0; y--)
            {
                bool full = true;
                for (int x = 0; x < Width; x++)
                {
                    if (grid[y, x] == 0)
                    {
                        full = false;
                        break;
                    }
                }
                if (full)
                {
                    // Сдвигаем все строки выше на одну вниз
                    for (int yy = y; yy > 0; yy--)
                        for (int x = 0; x < Width; x++)
                            grid[yy, x] = grid[yy - 1, x];
                    // Верхнюю строку очищаем
                    for (int x = 0; x < Width; x++)
                        grid[0, x] = 0;
                    lines++;
                    y++; // ещё раз проверим эту же строку (теперь в ней оказались сдвинутые данные)
                }
            }
            return lines;
        }
    }

    // Главный игровой класс
    public class Game
    {
        private Field field;
        private Figure currentFigure;
        private Random rnd = new Random();
        private int score;
        private const int DropInterval = 2000; // 2 секунды на одно падение

        // Генерация случайной фигуры в начальной позиции
        private Figure GenerateRandomFigure()
        {
            // Все 7 стандартных тетромино в начальных ориентациях
            List<bool[,]> shapes = new List<bool[,]>
            {
                new bool[,] { {false, false, false, false},
                              {true,  true,  true,  true},
                              {false, false, false, false},
                              {false, false, false, false} },  // I
                new bool[,] { {true, true},
                              {true, true} },                  // O
                new bool[,] { {false, true, false},
                              {true,  true, true},
                              {false, false, false} },         // T
                new bool[,] { {false, true, true},
                              {true,  true, false},
                              {false, false, false} },         // S
                new bool[,] { {true,  true, false},
                              {false, true, true},
                              {false, false, false} },         // Z
                new bool[,] { {true, false, false},
                              {true, true,  true},
                              {false, false, false} },         // J
                new bool[,] { {false, false, true},
                              {true,  true,  true},
                              {false, false, false} }          // L
            };

            bool[,] shape = shapes[rnd.Next(shapes.Count)];
            int startX = (Field.Width - shape.GetLength(1)) / 2;
            int startY = 0;
            return new Figure(shape, startX, startY);
        }

        // Фиксация текущей фигуры и создание новой
        private void FixAndNewFigure(ref bool gameOver)
        {
            field.FixFigure(currentFigure);
            int lines = field.ClearLines();
            score += lines * 100;

            currentFigure = GenerateRandomFigure();
            if (!field.IsValidPosition(currentFigure))
                gameOver = true;
        }

        // Отрисовка поля, рамки и текущей фигуры
        private void Draw()
        {
            Console.Clear();

            // Временный массив для визуализации (поле + активная фигура)
            int[,] render = new int[Field.Height, Field.Width];
            for (int y = 0; y < Field.Height; y++)
                for (int x = 0; x < Field.Width; x++)
                    render[y, x] = field.IsOccupied(x, y) ? 1 : 0;

            if (currentFigure != null)
            {
                for (int i = 0; i < currentFigure.Height; i++)
                    for (int j = 0; j < currentFigure.Width; j++)
                        if (currentFigure.Shape[i, j])
                        {
                            int absX = currentFigure.X + j;
                            int absY = currentFigure.Y + i;
                            if (absY >= 0 && absY < Field.Height && absX >= 0 && absX < Field.Width)
                                render[absY, absX] = 1;
                        }
            }

            // Верхняя граница рамки
            Console.Write("┌");
            for (int x = 0; x < Field.Width; x++) Console.Write("─");
            Console.WriteLine("┐");

            // Строки поля
            for (int y = 0; y < Field.Height; y++)
            {
                Console.Write("│");
                for (int x = 0; x < Field.Width; x++)
                    Console.Write(render[y, x] == 1 ? '█' : ' ');
                Console.WriteLine("│");
            }

            // Нижняя граница рамки
            Console.Write("└");
            for (int x = 0; x < Field.Width; x++) Console.Write("─");
            Console.WriteLine("┘");

            Console.WriteLine($"Score: {score}");
        }

        // Запрос на перезапуск игры
        private bool AskRestart()
        {
            Console.WriteLine("Press Enter to play again or Esc to exit.");
            while (true)
            {
                var key = Console.ReadKey(true).Key;
                if (key == ConsoleKey.Enter) return true;
                if (key == ConsoleKey.Escape) return false;
            }
        }

        // Запуск основного цикла игры
        public void Run()
        {
            bool restart = true;
            while (restart)
            {
                restart = false;
                field = new Field();
                score = 0;
                currentFigure = GenerateRandomFigure();

                // Если первая же фигура не помещается – сразу Game Over
                if (!field.IsValidPosition(currentFigure))
                {
                    Console.Clear();
                    Console.WriteLine("Game Over! Unable to place the first figure.");
                    restart = AskRestart();
                    continue;
                }

                Stopwatch stopwatch = Stopwatch.StartNew();
                bool gameOver = false;

                while (!gameOver)
                {
                    // Обработка нажатий клавиш
                    while (Console.KeyAvailable)
                    {
                        var key = Console.ReadKey(true).Key;
                        switch (key)
                        {
                            case ConsoleKey.LeftArrow:
                                currentFigure.MoveLeft(field);
                                break;
                            case ConsoleKey.RightArrow:
                                currentFigure.MoveRight(field);
                                break;
                            case ConsoleKey.DownArrow:
                                if (!currentFigure.MoveDown(field))
                                    FixAndNewFigure(ref gameOver);
                                stopwatch.Restart();
                                break;
                            case ConsoleKey.Spacebar:
                                currentFigure.Rotate(field);
                                break;
                        }
                    }

                    // Автоматическое падение по таймеру (каждые 2 секунды)
                    if (stopwatch.ElapsedMilliseconds >= DropInterval)
                    {
                        if (!currentFigure.MoveDown(field))
                            FixAndNewFigure(ref gameOver);
                        stopwatch.Restart();
                    }

                    // Перерисовка экрана
                    Draw();

                    // Небольшая пауза для снижения нагрузки на CPU
                    Thread.Sleep(30);
                }

                // Сообщение о конце игры и предложение начать заново
                Console.SetCursorPosition(0, Field.Height + 2);
                Console.WriteLine("Game Over! Your score: " + score);
                restart = AskRestart();
            }
        }
    }

    // Точка входа
    class Program
    {
        static void Main(string[] args)
        {
            Console.OutputEncoding = System.Text.Encoding.UTF8; // для корректного отображения рамок
            Game game = new Game();
            game.Run();
        }
    }
}