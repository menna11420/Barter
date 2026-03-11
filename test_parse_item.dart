enum ItemCategory {
  electronics,
  clothing,
  books,
  furniture,
  sports,
  other,
  service
}

enum ItemCondition {
  newItem,
  likeNew,
  good,
  fair,
  poor
}

enum ItemType {
  product,
  service
}

class ItemModel {
  final String id;
  final String ownerId;
  final String ownerName;
  final String title;
  final String description;
  final List<String> imageUrls;
  final ItemCategory category;
  final ItemCondition condition;
  final String? preferredExchange;
  final String location;
  final double? latitude;
  final double? longitude;
  final String? detailedAddress;
  final DateTime createdAt;
  final bool isAvailable;
  final bool isExchanged;
  final ItemType itemType;
  final bool isRemote;

  ItemModel({
    required this.id,
    required this.ownerId,
    required this.ownerName,
    required this.title,
    required this.description,
    required this.imageUrls,
    required this.category,
    required this.condition,
    this.preferredExchange,
    required this.location,
    this.latitude,
    this.longitude,
    this.detailedAddress,
    required this.createdAt,
    this.isAvailable = true,
    this.isExchanged = false,
    this.itemType = ItemType.product,
    this.isRemote = false,
  });

  factory ItemModel.fromJson(Map<String, dynamic> json) {
    return ItemModel(
      id: json['id'] ?? '',
      ownerId: json['ownerId'] ?? '',
      ownerName: json['ownerName'] ?? '',
      title: json['title'] ?? '',
      description: json['description'] ?? '',
      imageUrls: List<String>.from(json['imageUrls'] ?? []),
      category: ItemCategory.values[json['category'] ?? 5],
      condition: ItemCondition.values[json['condition'] ?? 2],
      preferredExchange: json['preferredExchange'],
      location: json['location'] ?? '',
      latitude: json['latitude']?.toDouble(),
      longitude: json['longitude']?.toDouble(),
      detailedAddress: json['detailedAddress'],
      createdAt: json['createdAt'] != null
          ? DateTime.parse(json['createdAt'])
          : DateTime.now(),
      isAvailable: json['isAvailable'] ?? true,
      isExchanged: json['isExchanged'] ?? false,
      itemType: ItemType.values[json['itemType'] ?? 0],
      isRemote: json['isRemote'] ?? false,
    );
  }
}

Map<String, dynamic> _mapItemDto(Map<String, dynamic> json) {
  return {
    'id': json['id'],
    'ownerId': json['ownerId'],
    'title': json['title'],
    'description': json['description'],
    'imageUrls': json['imageUrls'],
    'category': _mapCategoryFromInt(json['category']),
    'condition': _mapConditionFromInt(json['condition']),
    'preferredExchange': json['preferredExchange'],
    'location': json['location'],
    'latitude': json['latitude'],
    'longitude': json['longitude'],
    'createdAt': json['createdAt'],
    'itemType': json['itemType'] == 1 ? 'service' : 'product',
    'isAvailable': json['isAvailable']
  };
}

String _mapCategoryFromInt(int cat) {
  switch(cat) {
    case 0: return 'Electronics';
    case 1: return 'Clothing';
    case 2: return 'Books';
    case 3: return 'Furniture';
    case 4: return 'Sports';
    case 6: return 'Service';
    default: return 'Other';
  }
}

String _mapConditionFromInt(int cond) {
  switch(cond) {
    case 0: return 'New';
    case 1: return 'Like New';
    case 2: return 'Good';
    case 3: return 'Fair';
    case 4: return 'Poor';
    default: return 'Good';
  }
}

void main() {
  final apiResponse = {
    "id": "c37121af-64fb-4863-8272-81a3cbce70d0",
    "ownerId": "526ba921-f3a4-44f7-9e42-aec94e8125ef",
    "ownerName": "menna",
    "title": "Node Js",
    "description": "Course Node Js",
    "imageUrls": ["https://via.placeholder.com/800"],
    "category": 6,
    "condition": 2,
    "preferredExchange": null,
    "location": "San Francisco",
    "latitude": 37.7,
    "longitude": -122.4,
    "detailedAddress": "1-99 Stockton",
    "createdAt": "2026-03-01T01:04:25.875331Z",
    "isAvailable": true,
    "isExchanged": false,
    "itemType": 1,
    "isRemote": false
  };

  print("Mapped parse:");
  try {
    ItemModel.fromJson(_mapItemDto(apiResponse));
    print("Mapped parse SUCCESS!");
  } catch (e, stack) {
    print("Mapped parse ERROR:");
    print(e);
  }
}
