#pragma once
class Point
{
public:
	int x;
	int y;
	Point(int x, int y);
	std::string toString() const;
};

long long SquareDistance(const Point& point1, const Point& point2) {
	long long x_dif = static_cast<long long>(point1.x) - point2.x;
	long long y_dif = static_cast<long long>(point1.y) - point2.y;
	return x_dif * x_dif + y_dif * y_dif;
}
