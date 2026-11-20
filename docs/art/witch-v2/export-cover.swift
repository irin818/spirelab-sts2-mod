import AppKit

let arguments = CommandLine.arguments
guard arguments.count == 3, let source = NSImage(contentsOfFile: arguments[1]) else {
    fatalError("Usage: swift export-cover.swift <selection-source> <output.png>")
}

let canvas = NSImage(size: NSSize(width: 960, height: 540), flipped: false) { bounds in
    source.draw(in: bounds)
    let ivory = NSColor(srgbRed: 0.95, green: 0.89, blue: 0.9, alpha: 1)
    let rose = NSColor(srgbRed: 0.85, green: 0.42, blue: 0.54, alpha: 1)
    func draw(_ text: String, x: CGFloat, y: CGFloat, font: NSFont, color: NSColor, tracking: CGFloat) {
        (text as NSString).draw(at: NSPoint(x: x, y: y), withAttributes: [
            .font: font, .foregroundColor: color, .kern: tracking
        ])
    }
    rose.setFill()
    NSBezierPath(rect: NSRect(x: 66, y: 301, width: 43, height: 2)).fill()
    draw("SPIRELAB", x: 66, y: 313, font: .monospacedSystemFont(ofSize: 13, weight: .medium), color: rose, tracking: 3)
    draw("THE WITCH", x: 64, y: 225, font: NSFont(name: "Palatino-Roman", size: 55)!, color: ivory, tracking: -0.5)
    draw("魔女", x: 67, y: 186, font: NSFont(name: "PingFangSC-Regular", size: 25)!, color: ivory, tracking: 5)
    return true
}
let png = NSBitmapImageRep(data: canvas.tiffRepresentation!)!.representation(using: .png, properties: [:])!
try png.write(to: URL(fileURLWithPath: arguments[2]))
