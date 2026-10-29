
const canvas = document.getElementById("gameCanvas");
const ctx = canvas.getContext("2d");

const width = canvas.width;
const height = canvas.height;

const scoreElement = document.getElementById("gameScore");
const livesElement = document.getElementById("gameLives");
const bestElement = document.getElementById("gameBest");

const startButton = document.getElementById("startGame");
const pauseButton = document.getElementById("pauseGame");
const restartButton = document.getElementById("restartGame");

const player = {
    x: width / 2 - 65,
    y: height - 40,
    width: 130,
    height: 20,
    speed: 600
};

const keys = {
    left: false,
    right: false
};

let tickets = [];
let score = 0;
let lives = 3;
let best = Number(localStorage.getItem("movieCatcherBest")) || 0;
let running = false;
let paused = false;
let gameOver = false;
let spawnTimer = 0;
let lastTime = 0;

bestElement.textContent = best;

document.addEventListener("keydown", (event) => {
    const key = event.key.toLowerCase();

    if (["arrowleft", "arrowright", "a", "d"].includes(key)) {
        event.preventDefault();
    }

    if (key === "arrowleft" || key === "a") {
        keys.left = true;
    }

    if (key === "arrowright" || key === "d") {
        keys.right = true;
    }

    if (key === "p" && !event.repeat) {
        togglePause();
    }
});

document.addEventListener("keyup", (event) => {
    const key = event.key.toLowerCase();

    if (key === "arrowleft" || key === "a") {
        keys.left = false;
    }

    if (key === "arrowright" || key === "d") {
        keys.right = false;
    }
});

window.addEventListener("blur", () => {
    keys.left = false;
    keys.right = false;
});

function resetGame() {
    player.x = width / 2 - player.width / 2;
    tickets = [];
    score = 0;
    lives = 3;
    spawnTimer = 0;
    running = true;
    paused = false;
    gameOver = false;

    scoreElement.textContent = score;
    livesElement.textContent = lives;

    startButton.disabled = true;
    pauseButton.disabled = false;
    pauseButton.textContent = "Pause";
}

function togglePause() {
    if (!running) return;

    paused = !paused;
    pauseButton.textContent = paused ? "Resume" : "Pause";
}

function spawnTicket() {
    const size = 30;

    tickets.push({
        x: Math.random() * (width - size),
        y: -size,
        width: size,
        height: 22,
        speed: 120 + Math.min(score * 1.0, 150)
    });
}

function update(deltaTime) {
    if (!running || paused) return;

    if (keys.left) {
        player.x -= player.speed * deltaTime;
    }

    if (keys.right) {
        player.x += player.speed * deltaTime;
    }

    player.x = Math.max(0, Math.min(player.x, width - player.width));

    spawnTimer += deltaTime;

    if (spawnTimer >= Math.max(0.45, 1.2 - score / 300)) {
        spawnTicket();
        spawnTimer = 0;
    }

    for (let i = tickets.length - 1; i >= 0; i--) {
        const ticket = tickets[i];

        ticket.y += ticket.speed * deltaTime;

        const collision =
            ticket.x < player.x + player.width &&
            ticket.x + ticket.width > player.x &&
            ticket.y < player.y + player.height &&
            ticket.y + ticket.height > player.y;

        if (collision) {
            tickets.splice(i, 1);
            score += 10;
            scoreElement.textContent = score;

            if (score > best) {
                best = score;
                bestElement.textContent = best;
                localStorage.setItem("movieCatcherBest", best);
            }

            continue;
        }

        if (ticket.y > height) {
            tickets.splice(i, 1);
            lives--;
            livesElement.textContent = lives;

            if (lives <= 0) {
                running = false;
                gameOver = true;
                startButton.disabled = false;
                pauseButton.disabled = true;
                break;
            }
        }
    }
}

function draw() {
    ctx.clearRect(0, 0, width, height);

    ctx.fillStyle = "#60a5fa";
    ctx.fillRect(player.x, player.y, player.width, player.height);

    for (const ticket of tickets) {
        ctx.fillStyle = "#fbbf24";
        ctx.fillRect(ticket.x, ticket.y, ticket.width, ticket.height);

        ctx.fillStyle = "#78350f";
        ctx.font = "18px Arial";
        ctx.fillText("★", ticket.x + 7, ticket.y + 18);
    }

    if (!running || paused) {
        ctx.fillStyle = "rgba(0, 0, 0, 0.6)";
        ctx.fillRect(0, 0, width, height);

        ctx.fillStyle = "#ffffff";
        ctx.textAlign = "center";
        ctx.font = "bold 36px Arial";

        const message = gameOver
            ? "GAME OVER"
            : paused
                ? "PAUSED"
                : "MOVIE CATCHER";

        ctx.fillText(message, width / 2, height / 2);

        ctx.font = "18px Arial";
        ctx.fillText(
            gameOver ? `Final score: ${score}` : "Catch the falling tickets!",
            width / 2,
            height / 2 + 35
        );

        ctx.textAlign = "left";
    }
}

function gameLoop(timestamp) {
    const deltaTime = Math.min((timestamp - (lastTime || timestamp)) / 1000, 0.05);
    lastTime = timestamp;

    update(deltaTime);
    draw();

    requestAnimationFrame(gameLoop);
}

startButton.addEventListener("click", resetGame);
restartButton.addEventListener("click", resetGame);
pauseButton.addEventListener("click", togglePause);

requestAnimationFrame(gameLoop);
