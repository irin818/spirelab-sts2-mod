import AppKit

let source = NSImage(contentsOfFile: "docs/art/witch/select-source.png")!
let width = 1200
let height = 675
let canvas = NSImage(size: NSSize(width: width, height: height), flipped: false) { bounds in
    source.draw(in: bounds)
    let warmSilver = NSColor(srgbRed: 0.89, green: 0.85, blue: 0.83, alpha: 1)
    let mutedRed = NSColor(srgbRed: 0.86, green: 0.32, blue: 0.39, alpha: 1)
    func draw(_ text: String, x: CGFloat, y: CGFloat, font: NSFont, color: NSColor, tracking: CGFloat) {
        (text as NSString).draw(at: NSPoint(x: x, y: y), withAttributes: [
            .font: font, .foregroundColor: color, .kern: tracking
        ])
    }
    mutedRed.setFill()
    NSBezierPath(rect: NSRect(x: 74, y: 394, width: 50, height: 2)).fill()
    draw("SPIRELAB", x: 74, y: 406, font: .monospacedSystemFont(ofSize: 15, weight: .medium), color: mutedRed, tracking: 3)
    draw("THE WITCH", x: 70, y: 285, font: NSFont(name: "Palatino-Roman", size: 70)!, color: warmSilver, tracking: -0.8)
    draw("魔女", x: 75, y: 241, font: NSFont(name: "PingFangSC-Regular", size: 28)!, color: warmSilver, tracking: 7)
    return true
}
let jpeg = NSBitmapImageRep(data: canvas.tiffRepresentation!)!.representation(using: .jpeg, properties: [.compressionFactor: 0.88])!
try jpeg.write(to: URL(fileURLWithPath: "docs/art/witch/workshop-cover.jpg"))
