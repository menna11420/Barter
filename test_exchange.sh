#!/bin/bash
EMAIL2="testex_1772581854@gmail.com"  # Using the one I just made

RES=$(curl -s -X POST http://localhost:5071/api/auth/login \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$EMAIL2\",\"password\":\"StrongPass123\"}")
TOKEN=$(echo $RES | grep -o '"token":"[^"]*' | cut -d'"' -f4)

# Propose exchange. Let's send empty offered/requested lists just to see if it hits the 500 error or validation error.
curl -s -v -X POST http://localhost:5071/api/exchanges \
  -H "Authorization: Bearer $TOKEN" \
  -H "Content-Type: application/json" \
  -d '{
        "proposedTo": "'$EMAIL2'",
        "itemsOffered": [{"itemId": "item1", "title": "t", "imageUrl": "t"}],
        "itemsRequested": [{"itemId": "item2", "title": "t", "imageUrl": "t"}],
        "notes": "Testing"
      }'
