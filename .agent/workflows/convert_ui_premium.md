---
description: Apply antiGGGGravity PREMIUM UI Branding Standards (Header, Footer, Typography) to Single, Dual, or Triple panel WPF windows.
---

# antiGGGGravity UI Branding Standards (Premium)

Use this workflow to apply the premium antiGGGGravity branding to any tool, regardless of layout complexity.

## 🏗️ Layout Options

### 1. Single Panel (Settings/Filters)
```
┌─────────────────────────────────────────────────────────────┐
│ 🔝 HEADER (Gradiant, 70px)                                  │
├─────────────────────────────────────────────────────────────┤
│ 🟦 MAIN CONTENT (Scrollable)                                │
│ ┌─────────────────────────────────────────────────────────┐ │
│ │                                                         │ │
│ │                    Full Width Content                   │ │
│ │                                                         │ │
│ └────── SectionBorder ────────────────────────────────────┘ │
├─────────────────────────────────────────────────────────────┤
│ 🔚 FOOTER (Light Gray, 70px)                                │
└─────────────────────────────────────────────────────────────┘
```

## 📐 Grid Structure

| Row | Height | Component |
| :--- | :--- | :--- |
| **0** | `70` | Premium Header |
| **1** | `*` | Main Body Content |
| **2** | `Auto` | Action Footer |

## 🔝 Premium Header Standard
```xml
<Border Grid.Row="0" Background="{StaticResource BrandGradientBrush}" CornerRadius="12,12,0,0">
    <Grid Margin="25,0">
        <StackPanel Orientation="Horizontal" VerticalAlignment="Center">
            <TextBlock Style="{StaticResource CompanyLogoStyle}"/>
            <TextBlock Style="{StaticResource CompanyDividerStyle}"/>
            <StackPanel VerticalAlignment="Center">
                <TextBlock Text="PREMIUM TOOL" Style="{StaticResource PremiumTitleStyle}"/>
                <TextBlock Text="Tool subtitle or active state" Style="{StaticResource PremiumSubtitleStyle}"/>
            </StackPanel>
        </StackPanel>
        <Button HorizontalAlignment="Right" VerticalAlignment="Center" Content="✕" 
                Style="{StaticResource PremiumIconButtonStyle}" Width="30" Height="30"
                Click="Close_Click" WindowChrome.IsHitTestVisibleInChrome="True"/>
    </Grid>
</Border>
```

## 🔚 Standard Footer Standard
```xml
<Border Grid.Row="2" Padding="20,15" Background="#FAFAFA" CornerRadius="0,0,12,12" BorderBrush="{StaticResource PremiumBorderBrush}" BorderThickness="0,1,0,0">
    <Grid>
        <Grid.ColumnDefinitions>
            <ColumnDefinition Width="*"/>
            <ColumnDefinition Width="Auto"/>
            <ColumnDefinition Width="Auto"/>
        </Grid.ColumnDefinitions>
        
        <TextBlock Text="Ready to innovate..." VerticalAlignment="Center" Foreground="#999999"/>
        <Button x:Name="UI_Button_Cancel" Grid.Column="1" Content="Cancel" Style="{StaticResource PremiumPrimaryButtonStyle}" Width="90" Margin="0,0,12,0"/>
        <Button x:Name="UI_Button_Apply" Grid.Column="2" Content="Apply" Style="{StaticResource PremiumActionButtonStyle}" Width="100"/>
    </Grid>
</Border>
```

## 🎨 Design System
- **Colors**: Use `BrandGradientBrush` for headers and `PremiumActionButtonStyle` for primary actions.
- **Section containers**: Wrap body content in a Border with `Background="#FAFAFA"` and `CornerRadius="8"`.
- **Typography**: Headers: 14px SemiBold | Labels: 12px Regular | Subtitles: 11px Light.

---

## 🚀 Reusable Prompt (Premium Conversion)

Use this prompt to trigger an ultra-premium UI conversion:

> **Apply Premium UI Standards**: Harmonize the tool using the `/convert_ui_premium` workflow.
> 
> 1. **Resource Dictionary**: Ensure `Pre_BrandStyles.xaml` is merged in `Window.Resources`.
> 2. **Window Frame**: Use `WindowStyle="None"`, `AllowsTransparency="True"`, and `WindowChrome`.
> 3. **Premium Header**: Implement the gradient header with company logo and close button.
> 4. **Modern Footer**: 3-column footer with status text and action buttons.
