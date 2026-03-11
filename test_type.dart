enum ItemCategory { electronics, clothing, books, furniture, sports, other, service }
void main() {
  Map<String, dynamic> json = {'category': 'Electronics'};
  try {
    var category = ItemCategory.values[json['category'] ?? 5];
  } catch (e, stack) {
    print("ERROR CAUGHT:");
    print(e);
    print(stack);
  }
}
