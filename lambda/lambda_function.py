import json
import urllib.request
import urllib.error

API_URL = "http://13.60.182.225/api/movies"


def fetch_movies():
    with urllib.request.urlopen(API_URL, timeout=8) as resp:
        return json.loads(resp.read().decode("utf-8"))


def response(status, data):
    return {
        "statusCode": status,
        "headers": {"Content-Type": "application/json; charset=utf-8"},
        "body": json.dumps(data, ensure_ascii=False),
    }


def lambda_handler(event, context):
    path = event.get("rawPath", "/").rstrip("/") or "/"
    method = event["requestContext"]["http"]["method"]

    if method != "GET":
        return response(405, {"error": "Method not allowed"})

    try:
        movies = fetch_movies()
    except (urllib.error.URLError, TimeoutError, ValueError):
        return response(502, {"error": "FilmHub API is unavailable"})

    if path in ("/", "/movies"):
        return response(200, {"count": len(movies), "items": movies})

    if path.startswith("/movies/"):
        movie_id = path.split("/")[-1]
        for movie in movies:
            if str(movie["id"]) == movie_id:
                return response(200, movie)
        return response(404, {"error": "Movie not found"})

    if path == "/stats":
        counts = {}
        for movie in movies:
            for genre in movie["genres"]:
                counts[genre["name"]] = counts.get(genre["name"], 0) + 1
        by_genre = dict(sorted(counts.items(), key=lambda x: -x[1]))
        return response(200, {"total": len(movies), "byGenre": by_genre})

    return response(404, {"error": "Not found"})