# ESPN API Endpoints

## Core ESPN APIs

### Get Games/Schedule for a Week
```
https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{year}/types/{seasonType}/weeks/{week}/events
```
- `year`: Season year (e.g., 2025)
- `seasonType`: 1=Preseason, 2=Regular Season, 3=Postseason  
- `week`: Week number (1-18 for regular season)

### Get Box Score/Game Summary
```
https://site.api.espn.com/apis/site/v2/sports/football/nfl/summary?event=401772814
```
- `eventId`: ESPN game ID (e.g., 401772814)

### Get Betting Odds
```
https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/events/{gameId}/competitions/{competitionId}/odds
```

### Get NFL Teams
```
https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/teams
```

### Get Team Roster
```
https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/seasons/{year}/teams/{teamId}/athletes
```

### Get Individual Player
```
https://sports.core.api.espn.com/v2/sports/football/leagues/nfl/athletes/{playerId}
```

## Web Scraping Endpoints

### Scoreboard (Web)
```
https://www.espn.com/nfl/scoreboard/_/week/{week}/year/{year}/seasontype/{seasonType}
```

### Schedule (Web)
```
https://www.espn.com/nfl/schedule/_/week/{week}/year/{year}/seasontype/{seasonType}
```

### Box Score (Web)
```
https://www.espn.com/nfl/boxscore/_/gameId/{eventId}
```